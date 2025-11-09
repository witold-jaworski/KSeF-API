using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Core.Models.Authorization;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;

namespace KSeF.Services.Api
{
	//Pomocnicza klasa dla klas obsługi żądań, implementująca parę podstawowych rzeczy
	internal abstract class HandlerBase : IRequestHandler
	{

		//Struktura z kluczem symetrycznym, używanym do szyfrowania / odszyfrowywania faktur.
		public class CipherData
		{
			public string Base64Key { get; set; } = ""; //Klucz symetryczny, enkodowany w Base64
			public string Base64Mix { get; set; } = ""; //towarzysząca "sól", enkodowana w Base64
		}

		//Struktura, w której zwracamy metadane certyfikatu
		public class CertificateMetadata
		{
			public string? Name { get; set; }
			public required string SerialNumber { get; set; }
			public required DateOnly ValidFrom { get; set; }
			public required DateOnly ValidTo { get; set; }
			public Dictionary<string, string> Subject { get; set; } = []; //Osobiste PESEL i NIP są maskowane "u źródła"
			public Dictionary<string, string> Issuer { get; set; } = [];
			public string? PublicKeyOid { get; set; }
			public required string Fingerprint { get; set; }
			public required bool HasPrivateKey { get; set; }
			public X509ExtensionCollection Extensions { get; set; } = [];
		}

		//Stałe, wykorzystywane w różnych klasach potomnych
		protected const string NIP_XPATH = "//*[local-name() = 'Podmiot1']/*/*[local-name()='NIP']"; //Ścieżka w FA do NIP-u sprzedawcy
		protected const string P_1_XPATH = "//*[local-name() = 'Fa']/*[local-name()='P_1']"; //Ścieżka w FA do daty wystawienia faktury

		//pola klasy
		protected string? _request;
		protected IKSeFClient? _ksefClient;

		private IServiceProvider? _services;
		private ILogger? _logger;

		//Pomocnicza właściwość, ułatwiająca żądania usług ("GetService") w klasach potomnych
		protected IServiceProvider Scope
		{
			get
			{
				if (_services == null) throw new InvalidOperationException("ASSERT:handler has not bee assigned to a scope, yet");
				return _services;
			}
		}

		//Pomocnicza właściwość, udostępniająca log programu
		protected ILogger Logger
		{
			get
			{
				if (_services != null && _logger == null) _logger = _services.GetRequiredService<ILogger<HandlerBase>>();
				if (_logger == null) throw new InvalidOperationException("ASSERT:no logger service available (yet?)");
				return _logger;
			}
		}

		//Implementacja IRequestHandler:
		public abstract bool RequiresInput { get; }
		public abstract bool HasResults { get; }

		public void Assign(string request, IServiceProvider services)
		{
			_request = request;
			_services = services;
			if (Program.IsRunningLocal == false)
				_ksefClient = services.GetRequiredService<IKSeFClient>(); //wywoła wyjątek, gdy interfejs nie istnieje
																		  //a gdy działamy lokalnie - _ksefClient będzie null
		}

		public abstract Task PrepareInputAsync(string data, CancellationToken stopToken);
		public abstract Task ProcessAsync(CancellationToken stopToken);
		public abstract string SerializeResults();

		//---- Metody pomocnicze, wykorzystywane przez klasy potomne (aby nie duplikować kodu) ----

		//Ładuje certyfikat, który może być podany na różne sposoby (plik/string PEM),
		//w "jednym kawałku" albo z wydzielonym kluczem prywatnym.
		//Argumenty:
		//	certFile:		ścieżka do pliku z certyfikatem (PEM lub PFX)
		//	certPem:		tekst certyfikatu PEM
		//	pkeyFile:		ścieżka do pliku z kluczem prywatnym (PEM)
		//	pkeyPem:		tekst klucza prywatnego (PEM)
		//	forReviewOnly:	nie zgłaszaj wyjątków, np. gdy certyfikat jest przeterminowany - otwieramy go tylko w celu sprawdzenia.
		//  pwd:			hasło do klucza prywatnego (jeżeli jest zaszyfrowany)
		//Argumenty można podawać na wiele sposobów, np. plik z certyfikatem i tekst klucza prywatnego (o ile nie ma go w certyfikacie)
		protected X509Certificate2 LoadCertificate(string? certFile, string? certPem, string? pkeyFile, string? pkeyPem, bool forReviewOnly = false, string pwd = "")
		{
			//Czy to PFX?
			if (certFile != null && Path.GetExtension(certFile) == ".pfx")	return X509CertificateLoader.LoadPkcs12FromFile(certFile, pwd == "" ? null : pwd);

			//Nie, w takim razie przyjmuję, że to PEM:
			string certText;
			string? pkeyText = null;
			X509Certificate2 result;

			if (certFile != null) certText = File.ReadAllText(ValidateForInput(certFile, "certificateFile"));
			else if (certPem != null) certText = certPem;
			else throw new ArgumentException("Missing certificate reference", "certificatePem");

			if (pkeyFile != null) pkeyText = File.ReadAllText(ValidateForInput(pkeyFile, "privateKeyFile"));
			else if (pkeyPem != null) pkeyText = pkeyPem;


			if (pkeyText == null) //w takim przypadku spróbujmy wydobyć klucz z pliku certyfikatu:
			{
				pkeyText = certText.GetPemSection(PemSection.PRIVATE_KEY);
			}

			if (pkeyText == null) return X509Certificate2.CreateFromPem(certText); //Cóż, zwróć certyfikat bez klucza 

			//OK, mamy i certyfikat, i klucz:
			if (pwd.Length == 0)
				result = X509Certificate2.CreateFromPem(certText, pkeyText);
			else
				result = X509Certificate2.CreateFromEncryptedPem(certText, pkeyText, pwd); //Zgłosi wyjątek, gdy hasło się nie zgadza


				//Parę szczegółów do logu:
				Logger.LogDebug("Loaded certificate:\n\tSerial number:\t{SN}\n\tValid from:\t{validFrom}\n\tValid to:\t{validTo}",
															result.SerialNumber, result.NotBefore.ToString("yyyy-MM-dd"), result.NotAfter.ToString("yyyy-MM-dd"));
			if (forReviewOnly == false)
			{//Sprawdź od razu, czy certyfikat jest ważny:
				if (result.NotBefore > DateTime.Now) throw new InvalidDataException($"Certificate is not valid (it starts from {result.NotBefore:yyyy-MM-dd})");
				if (result.NotAfter < DateTime.Now) throw new InvalidDataException($"Certificate is not valid (it expired on {result.NotAfter:yyyy-MM-dd})");
			}
			return result;
		}

		//Zwraca tekst XML (niepodpisanego) żądania uwierzytelnienia KSeF
		//Argumenty:
		//	challenge:				identyfikator, otrzymany z GetAuthChallenge()
		//	nip, iid, nipVatUe:		identyfikator kontekstu (przedsiębiorstwa) - jeden z nich musi być niepusty
		//	useFingertip:			opcjonalne. Gdy != true, to <SubjectIdentifierType> w wynikowej strukturze = "certificateSubject"
		protected static string BuildAuthRequest(string challenge, string? nip, string? iid, string? nipVatUe, bool useFingertip = false)
		{
			AuthenticationTokenContextIdentifierType contextType;
			string contextId;
			if (nip != null)
			{
				contextType = AuthenticationTokenContextIdentifierType.Nip;
				contextId = nip;
			}
			else if (iid != null)
			{
				contextType = AuthenticationTokenContextIdentifierType.InternalId;
				contextId = iid;
			}
			else if (nipVatUe != null)
			{
				contextType = AuthenticationTokenContextIdentifierType.NipVatUe;
				contextId = nipVatUe;
			}
			else throw new MissingMemberException("Missing context identifier. You must provide one of these fields: 'nip', 'iid', or 'nipVatUe'");

			//Build the request XML in the memory:
			var authRequest = AuthTokenRequestBuilder
							   .Create()
							   .WithChallenge(challenge)
							   .WithContext(contextType, contextId)
							   .WithIdentifierType(useFingertip ? AuthenticationTokenSubjectIdentifierTypeEnum.CertificateFingerprint
																	  : AuthenticationTokenSubjectIdentifierTypeEnum.CertificateSubject)
							   .Build();

			return AuthenticationTokenRequestSerializer.SerializeToXmlString(authRequest);
		}

		//Zwraca pełną ścieżkę do jakiegoś pliku wynikowego
		//rozwija ewentualną ścieżkę względną w bezwzględną, sprawdza czy istnieje folder, w którym ma powstać ten plik.
		//fieldName to nazwa sprawdzanego pola. Możesz ją podać, gdy chcesz, aby pojawiała się w komunikacie o błędzie.
		protected static string ValidateForOutput(string path, string? fieldName = null)
		{
			var fullPath = Program.FullPath(path);
			var dir = Path.GetDirectoryName(fullPath);
			if (Path.Exists(dir) == false)
				throw new DirectoryNotFoundException($"Directory{fieldName ?? $" specified in '{fieldName}' field"} does not exists\n('{dir}')");
			return fullPath;
		}

		//Zwraca pełną ścieżkę do jakiegoś pliku wejściowego
		//rozwija ewentualną ścieżkę względną w bezwzględną, sprawdza czy istnieje wskazany plik.
		//fieldName to nazwa sprawdzanego pola. Możesz ją podać, gdy chcesz, aby pojawiała się w komunikacie o błędzie.
		protected static string ValidateForInput(string? path, string? fieldName = null)
		{
			if (path == null) throw new ArgumentNullException($"Missing required field{fieldName ?? $" '{fieldName}'"}");
			var fullPath = Program.FullPath(path);
			if (Path.Exists(fullPath) == false)
				throw new FileNotFoundException($"File {fieldName ?? $" specified in '{fieldName}' field"} does not exists\n('{fullPath}')");
			return fullPath;
		}

		//Obsługuje powtarzające sie sytuacje załadowania danych, które mogą sie znajdować
		//albo we wskazanym pliku,
		//albo są przesłane w danych enkodowanych w Base64.
		//Trzeci parametr - fieldOfPath - to opcjonalna nazwa pola ze ścieżką do pliku (w przypadku braku obydwu pól upominamy się o plik)
		protected static byte[] GetBytes(string? base64, string? filePath, string? fieldOfPath = null)
		{
			if (base64 == null)
				return File.ReadAllBytes(ValidateForInput(filePath, fieldOfPath));
			else
				return Convert.FromBase64String(base64);
		}

		//Ładuje XML z ciągu bajtów (<bytes> to dane binarne, np. odczytane z pliku *.xml)
		protected static XmlDocument XmlFromBytes(byte[] bytes)
		{
			var xml = new XmlDocument();
			using (var ms = new MemoryStream(bytes))
			{ xml.Load(ms); }
			return xml;
		}

		//Zamienia dowolne hasło na parametry dla AES256 
		//Argumenty:
		//	pwd		tekst hasła
		//Zwraca: key (32 bajty), iv (16 bajtów)
		protected static (byte[] key, byte[] iv) ToAes256Parameters(string pwd)
		{
			var key = SHA256.HashData(Encoding.UTF8.GetBytes(pwd));
			//Dopełnij tekst do 16:
			while (pwd.Length < 16)
			{
				if (pwd.Length < 8) pwd += pwd;
				else pwd += pwd[^(16-pwd.Length)..];
			}
			pwd = pwd[^16..]; //baza dla iv to ostatnie 16 znaków hasła
			var iv = Encoding.UTF8.GetBytes(pwd);
			return (key, iv);
		}

		//Pomocnicza funkcja, zwracająca np. XML z UPO
		//Argumenty:
		//	restClient:		klient Rest (to wywołanie występuje tylko w kilku klasach, nie chcę pobierać IRestClient w konstruktorze)
		//	url:			url zasobu.
		//	stopToken:		opcjonalny, przekazuj z metody wywołującej
		protected static async Task<string> DownloadTextAsync(IRestClient restClient, Uri url, 
															CancellationToken stopToken = default)
		{
			return await restClient.SendAsync<string, object>(HttpMethod.Get, url.PathAndQuery, 
																						cancellationToken: stopToken).ConfigureAwait(false);
		}

		//Zwraca metadane certyfikatu w postaci odpowiedniej struktury
		//Argumenty:
		//	cert:	certyfikat
		protected static CertificateMetadata ReadCertificateMetadata(X509Certificate2 cert)
		{
			//Maskowanie numerów personalnych:
			var mask = new String('?', 10);
			var subject = cert.Subject;

			//Maskowanie danych RODO-wrażliwych:
			foreach (var pharse in "PNOPL-;NIP-;TIN-".Split(';'))
			{
				var pos = subject.IndexOf(pharse);
				if (pos > 0)
				{
					pos += pharse.Length;
					subject = string.Concat(subject.AsSpan(0, pos), mask, subject.AsSpan(pos + mask.Length));
				}
			}

			CertificateMetadata result = new()
			{
				SerialNumber = cert.SerialNumber,
				ValidFrom = DateOnly.FromDateTime(cert.NotBefore),
				ValidTo = DateOnly.FromDateTime(cert.NotAfter),
				Subject = SplitIntoDictionary(subject, ',', '=', "src"),
				Issuer = SplitIntoDictionary(cert.Issuer, ',', '=', "src"),
				HasPrivateKey = cert.HasPrivateKey,
				Fingerprint = cert.Thumbprint,
				PublicKeyOid = $"{cert.PublicKey.Oid.Value} ({cert.PublicKey.Oid.FriendlyName})",
				Extensions = cert.Extensions
			};

			//Przyjazna nazwa nie zawsze istnieje:
			if (cert.FriendlyName != "") result.Name = cert.FriendlyName;
			else //W przypadku certyfikatów z dokumentów podpisanych podpisem kwalifikowanym
			{	 //nazwe można próbować wyciągnąć z "CN"
				string? name;
				if (result.Subject.TryGetValue("CN", out name)) result.Name = name;
				else
				if (result.Subject.TryGetValue("O", out name)) result.Name = name;
				else
				if (result.Subject.TryGetValue("SN", out name)) result.Name = name;
			}

			return result;
		}

		//Dzieli (odpowiedni) tekst na pary słownika - np. zawartość pola [Subject] certyfikatu
		//Argumenty:
		//	text:			źródłowy tekst
		//	entrySeparator:	znak rozdzielący od siebie poszczególne pary
		//	tupleSeparator: znak rodzielający klucz od wartości
		//	rawKey:			opcjonalny. Jeżeli podczas przetwarzania jakieś elementy zostały odrzucone
		//					- umieści w nim ten dodatkowy klucz z oryginalnym tekstem
		//Jeżeli tekst nie daje się podzielić na pary - zwraca pusty słownik
		protected static Dictionary<string, string> SplitIntoDictionary(string? text, char entrySeparator, char tupleSeparator, 
																													string rawKey = "")
		{
			Dictionary<string, string> result = [];
			if (text != null) 
			{
				bool skipped = false;
				//Stworzenie słownika z pola Subject 
				foreach (var item in text.Split(entrySeparator))
				{
					string[] entry = item.Split(tupleSeparator);
					var key = entry[0].Trim();
					if (entry.Length == 2 && !result.ContainsKey(key))
						 result.Add(key, entry[1].Trim());
					else skipped = true;
				}
				if (skipped && rawKey != "" && !result.ContainsKey(rawKey)) 
															result.Add(rawKey, text);
			}
			return result;
		}
	}
}
