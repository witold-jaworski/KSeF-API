using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.QRCode;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#GetCertificateQrLink")]
	internal class GetCertificateQrLink : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? InvoiceFile { get; set; } //wyznacz link dla tego pliku z fakturą
			public string? InvoiceBase64 { get; set; } //XML faktury, tylko binarnie - np. odczytany jako dane binarne z pliku i enkodowany w Base64
			public string? CertificateFile { get; set; } //ścieżka do pliku certyfikatu (*.pem)
			public string? CertificatePem { get; set; } //tekst certyfikatu PEM ("-----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----")
			public string? PrivateKeyFile { get; set; } //ścieżka do pliku z kluczem prywatnym (*.pem)
			public string? PrivateKeyPem { get; set; } //tekst certyfikatu PEM ("-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----")
			public string? SaveToPng { get; set; }  //ścieżka do wynikowego pliku *.png, w którym program ma zapisać kod QR 
			public int PixelsPerDot { get; set; } = 20; //rozmiar pojedynczego kwadratu QR (w pikselach) - potem jest zmniejszany, więc musi byc duży aby było wyraźnie.
			public int ImageSize { get; set; } = 490; //rozmiar kodu QR (w pikselach) - ta wartość zmniejsza rozmycie
			public string? CertificateSerial { get; set; } //numer seryjny certyfikatu KSeF (gdyby był przypadkiem inny, niż wpisany w certyfikat)
			public string? ContextNip {  get; set; }    //NIP, którego dotyczy certyfikat (podaj, gdy nie ma go w samym certyfikacie):
														//może się różnić od NIP na fakturze, gdy jest ona wystawiana przez pełnomocnika (np. biuro rachunkowe)
			public string? ContextIid { get; set; } //Identyfikator wewnętrzny, którego dotyczy certyfikat:
			public string? ContextNipVatUe { get; set; } //Numer rejestracji podatkowej podmiotu spoza Polski, którego dotyczy certyfikat:
		}
		/*UWAGI:
			1. "InvoiceFile" i "InvoiceBase64" to alternatywy
			2.	Jeżeli nie podano saveToPng, program zwróci tylko w url tekst linku, który można zakodowac w kodzie QR.
			3.	Certyfikaty i klucz prywatny w formacie PEM mogą być podane wprost, jako tekst (para CertificatePem + PrivateKeyPem)
				lub poprzez wskazanie zawierających je plików (para CertificateFile + PrivateKeyFile).
			4.	Jeżeli "privateKeyPem/File" jest pominięty, program zakłada, że klucz prywatny znajduje się także w tekście "certificatePem/File"
			5.	Brak jakiejkolwiek informacji o certyfikacie wywoła wyjątek. 
			6.	certificateSerial i contextNip są dodane "na wszelki wypdek", gdyby z jakichś przyczyn program nie potrafił odczytać ich z certyfikatu. 
		*/
		//Struktura danych wyjściowych:
		protected class Results
		{
			public string LinkUrl { get; set; } = string.Empty; //url - taki, jak należy przekazać do generatora kodu QR
			public string? PathToPng { get; set; } //opcjonalny: ścieżka do obrazu z kodem QR
		}
		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public string? nip;
			public string? invoiceHash;
			public DateTime issueDate; //dodana z myślą o GetCertificateQrLink
			public X509Certificate2? certificate;
			public string? certificateSerial;
			public QRCodeContextIdentifierType contextIdentifierType = QRCodeContextIdentifierType.Nip; //chyba nie bedzie innych przypadków
			public string? contextIdentifierValue;
			public string? saveToPng;
			public int imageSize = 300;
			public int pixelsPerDot = 2;
		}
		//----------------------
		protected Params _input = new();
		protected Results _output = new();

		//Pomocnicza metoda, do nadpisania przez klasę potomną GetInvoiceQrLink:
		//Zwraca tekst url, który ma być zakodowany w kodzie QR
		protected virtual string GenerateUrl(IVerificationLinkService linkService)
		{
			return linkService.BuildCertificateVerificationUrl(_input.nip,
																			_input.contextIdentifierType,
																			_input.contextIdentifierValue,
																			_input.certificateSerial,
																			_input.invoiceHash,
																			_input.certificate
																		);
		}
		//przygotowanie parametrów obrazu QR jest takie samo w obydwu handlerach, 
		//stąd wydzieliłem je we współdzieloną procedurę:
		protected void PrepareImageProcessing(string? imagePath, int pixelsPerDot, int imageSize)
		{
			//Jeżeli podano ścieżkę na kod QR: sprawdź,czy istnieje jej folder:
			if (imagePath != null)
			{
				_input.saveToPng = ValidateForOutput(imagePath, "saveToPng");
				_input.pixelsPerDot = pixelsPerDot;
				_input.imageSize = imageSize;
			}
		}

		//-- implementacja interfejsu:
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			//Zamień przekazaną faktruę na bajty:
			byte[] bytes = GetBytes(input.InvoiceBase64, input.InvoiceFile, "invoiceFile");

			//Oblicz jej hash:
			_input.invoiceHash = Scope.GetRequiredService<ICryptographyService>().GetMetaData(bytes).HashSHA;

			//Wyciagnij NIP z faktury:
			_input.nip = XmlFromBytes(bytes).SelectSingleNode(NIP_XPATH)?.InnerText;

			//Certyfikat: łączymy z kluczem klucz prywatnym (aby mniej parametrów było)
			_input.certificate = LoadCertificate(input.CertificateFile, input.CertificatePem, 
																					input.PrivateKeyFile, input.PrivateKeyPem);

			if (input.CertificateSerial != null)
 				_input.certificateSerial = input.CertificateSerial;
			else //Jak nie podano numeru certyfikatu wprost - użyj tego, który jest w niego wpisany:
				_input.certificateSerial = _input.certificate.SerialNumber;

			//Sprawa "kontekstu" (typ, wartość):

			if (input.ContextNipVatUe != null)
			{
				_input.contextIdentifierType = QRCodeContextIdentifierType.NipVatUe;
				_input.contextIdentifierValue = input.ContextNipVatUe;
			}
			else
			if (input.ContextIid != null)
			{
				_input.contextIdentifierType = QRCodeContextIdentifierType.InternalId;
				_input.contextIdentifierValue = input.ContextIid;
			}
			else
			if (input.ContextNip != null)
			{
				//Tu typu nie musze podawać, w deklaracji struktury jest ustwaiony domyślnie na .Nip
				_input.contextIdentifierValue = input.ContextNip;
			}
			else //Spróbuj określić numer NIP (wartość identyfikatora kontekstu) na podstawie wartości .Subject certyfikatu
			{//Z tekstu w rodzaju jak poniżej: 
			 //"Description=ZigZak NIP--1234563218, SERIALNUMBER=NIP-1234563218, L=Mazowieckie, C=PL, O=ZigZak, G=Jan, SN=Kowalski, CN=ZigZak"
			 //robimy słownik "klucz"="wartość":
				var subject = _input.certificate.Subject.Split(',')
															.Select(x => x.Split('='))
																			.ToDictionary(x => x[0].Trim(), x => x[1].Trim());

				//najpierw spróbuj odczytać regularny numer NIP organizacji z pieczęci firmowej (to specjalny klucz 2.5.4.97):
				string? rawValue;
				if (subject.TryGetValue("2.5.4.97", out rawValue) || subject.TryGetValue("OID.2.5.4.97", out rawValue))
				{
					if (rawValue.StartsWith("VATPL-")) // rawValue zawiera wyrażenie "VATPL-1234567890"
						_input.contextIdentifierValue = rawValue[6..]; //"1234567890"
				}

				//Coś z tego wyszło?
				if (_input.contextIdentifierValue == null)
					throw new MissingFieldException($"Cannot find the NIP of the context company in the certificate Subject ('{_input.certificate.Subject}')");
			}

			if (_input.contextIdentifierValue == null)
				throw new MissingFieldException($"Missing context identifier (none of the 'contextNip/Iid/NipVatUe' fields was specified)");

			PrepareImageProcessing(input.SaveToPng, input.PixelsPerDot, input.ImageSize);

			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			_output.LinkUrl = GenerateUrl(Scope.GetRequiredService<IVerificationLinkService>());
			if (_input.saveToPng != null)
			{
				byte[] bytes = QrCodeService.GenerateQrCode(_output.LinkUrl, _input.pixelsPerDot, _input.imageSize);
				File.WriteAllBytes(_input.saveToPng, bytes );

				_output.PathToPng = _input.saveToPng;
			}

			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
