using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services.Api.Local
{
	//Konwertuje niezaszyfrowany PEM z kluczem prywatnym na wskazany rodzaj certyfikatu
	[HandlesRequest("#CertificateFromPem")]
	internal class CertificateFromPem : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string CertificatePem { get; set; } //Certyfikat PEM z niezaszyfrowanym kluczem prywatnym
			public string? Password { get; set; }      //ewentualne hasło do zaszyfrowania klucza prywatnego
			public string? SaveTo { get; set; } //ścieżka do zapisu pliku z wynikowym certyfikatem
			public string? PrivateKeyFile { get; set; } //ewentualna ścieżka do zapisu pliku z kluczem prywatnym (*.key)
		}
		/* Uwagi:
			1.	Rozszerzenie ścieżki SaveTo decyduje o formacie zapisu certyfikatu. Dopuszczalne: *.pfx, *.p12, *.pem, *.crt. Jezeli nie podano -
				żądanie zwróci tekst certyfikatu (ewentualnie z zaszyfrowanym podanym hasłem kluczem prywatnym)
			2.	PrivateKeyFile jest ignorowane dla plików *.pfx, *.p12. Jeżeli nie zostało jawnie podane, to dla *.crt jest tworzony
				plik o takiej samej nazwie ale rozszerzeniu *.key. Dla pozostałych przypadków brak tego arguentu oznacza umieszczenie
				klucza prywatnego w pliku certyfikatu.
		*/
		//Struktura danych wyjściowych:
		protected class Results
		{
			public string? CertificatePem { get; set; } //Certyfikat w formacie PEM (zwracane, gdy nie podano SaveTo)
			public string? CertificateFile { get; set; } //ścieżka do wynikowego pliku certyfikatu (rozwinięcie SaveTo)
			public string? PrivateKeyFile { get; set; } //opcjonalna: ścieżka do wynikowego pliku z kluczem (jeżeli został zapisany odzielnie)
		}
		//Parametry wewnętrzne
		protected class Params
		{
			public string ext = "";			//rozszerzenie pliku (".crt" | ".pem" | ".pfx" | ".p12") - pełni rolę flagi sterującej
			public X509Certificate2? cert;	//Zapisywany certyfikat
			public string? path;			//ścieżka do zapisywanego pliku
			public string? keyPath;			//opcjonalna ścieżka do pliku z kluczem (dotyczy tylko *.cert i *.pem)
			public string? password;		//opcjonalne hasło
		}

		//----------------------
		protected Params _params = new();
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (inp.SaveTo != null)
			{
				_params.password = inp.Password;
				_params.path = ValidateForOutput(inp.SaveTo, "saveTo");
				_params.ext = Path.GetExtension(inp.SaveTo).ToLower();
				if (inp.PrivateKeyFile != null)
				{
					_params.keyPath = ValidateForOutput(inp.PrivateKeyFile, "privateKeyFile");
				}
				else if (_params.ext == ".crt") 
				{	//dla certyfikatów w plikach *.crt klucz prywatny jest w pliku o takiej samej nazwie, ale z rozszerzeniem *.key
					_params.keyPath = Path.Combine(Path.GetDirectoryName(_params.path) ?? "", Path.GetFileNameWithoutExtension(_params.path) + ".key" );
				}
				_params.cert = LoadCertificate(null, inp.CertificatePem,null,null);
			}
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_params.cert != null);
			switch(_params.ext)
			{
				case ".pfx":
				case ".p12":
					Debug.Assert(_params.path != null);
					File.WriteAllBytes(_params.path, _params.cert.Export(X509ContentType.Pfx, _params.password));
					_output.CertificateFile = _params.path;
					break;
				case "":
				case ".crt":
				case ".pem":
					var pem = _params.cert.ExportCertificatePem();
					string pkey = "";
					//Parametry wg AI Google 
					var pbe = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,	HashAlgorithmName.SHA256, 10000	);

					if (_params.cert.GetECDsaPublicKey() != null)
					{	
						var key = _params.cert.GetECDsaPrivateKey();
						Debug.Assert(key != null);
						
						if (_params.password == null)
							pkey = key.ExportECPrivateKeyPem();
						else
							pkey = key.ExportEncryptedPkcs8PrivateKeyPem(_params.password, pbe);
					}

					if (_params.cert.GetRSAPublicKey() != null)
					{
						var key = _params.cert.GetRSAPrivateKey();
						Debug.Assert(key != null);
						if (_params.password == null)
							pkey = key.ExportRSAPrivateKeyPem();
						else
							pkey = key.ExportEncryptedPkcs8PrivateKeyPem(_params.password, pbe);
					}

					if (pkey != null )
					{
						if(_params.keyPath != null)
						{
							File.WriteAllText(_params.keyPath, pkey);
							_output.PrivateKeyFile = _params.keyPath;
						}
						else //Dołącz do certyfikatu
						{
							pem += '\n' + pkey;
						}
					}

					if (_params.path == null) //to gdy ext = "". Liczę sie z tym, że w wyniku nieoczekiwanych danych pkey może być null
					{
						_output.CertificatePem = pem + (pkey == null ? "" : '\n'+pkey);
					}
					else //Podano ścieżkę do zapisania certyfikatu
					{	
						File.WriteAllText(_params.path, pem);
						_output.CertificateFile = _params.path;
					}
					break;
				default:
					throw new NotImplementedException($"Cannot save certificate into a '*.{_params.ext}' file");
			}
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
