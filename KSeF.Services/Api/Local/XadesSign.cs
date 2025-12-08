using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using KSeF.Client.Api.Services;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#XadesSign")]
	internal class XadesSign : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? SrcFile { get; set; } //ścieżka do pliku XML do podpisania (może być względna)
			public string? XmlContent { get; set; } //treść (XML) do podpisania
			public string? CertificateFile { get; set; } //ścieżka do pliku certyfikatu (*.pem lub *.pfx)
			public string? CertificatePem { get; set; } //tekst certyfikatu PEM ("-----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----")
			public string? PrivateKeyFile { get; set; } //ścieżka do pliku z kluczem prywatnym (*.pem)
			public string? PrivateKeyPem { get; set; } //tekst certyfikatu PEM ("-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----")
			public string? Password { get; set; }      //ewentualne hasło do klucza prywatnego
			public string? CertificateName { get; set; } //nazwa ("Friendly Name") certyfikatu
														 //w domyślnym magazynie lokalnym Windows
														 //aktualnie zalogowanego użytkownika
			public string? CertificateSn { get; set; } //Numer seryjny certyfikatu znajdującego się
													   //w domyślnym magazynie lokalnym Windows
													   //aktualnie zalogowanego użytkownika
			public string? DstFile { get; set; }    //ścieżka, w której ma być zapisany podpisany plik XML
			public string? Base64Content { get; set; }    //podpisany XML jako dane binarne, enkodowane w Base64
		}
		/* Uwagi:
		 *  1.  Dane wejściowe to albo ścieżka do pliku srcFile, albo tekst xmlContent.
			2.	Certyfikaty i klucz prywatny w formacie PEM mogą być podane wprost, jako tekst (para CertificatePem + PrivateKeyPem)
				lub poprzez wskazanie zawierających je plików (para CertificateFile + PrivateKeyFile).
			3.	Jeżeli "privateKeyPem/File" jest pominięty, program zakłada, że klucz prywatny znajduje się także w tekście "certificatePem/File"
			4.	Należy podać wartość "certificatePem/File"[+"privateKeyPem/File"] LUB "certificateName|certificateSn". To alternatywa. 
				Brak jakiejkolwiek informacji o certyfikacie wywoła wyjątek. 
			5.	Jeżeli nie podano DstFile, to podpisany XML jest zwracany jako dane binarne w base64Content
		*/

		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params 
		{ 
			public string xml = ""; //XML do podpisania (zawartość srcFile)
			public X509Certificate2? certificate; //certyfikat, którym mamy podpisać plik
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string? SignedFile { get; set; } //ścieżka do podpisanego pliku (tak - dla potwierdzenia)
			public string? Base64Content { get; set; } //podpisany XML jako dane binarne, enkodowane w Base64
		}
		/* Uwagi:
			Rezultat jest zwracany jako signedFile LUB base64Content, nigdy oba naraz
		*/

		//----------------------
		protected Params _input = new ();
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			if (inp.SrcFile != null)
				_input.xml = File.ReadAllText(ValidateForInput(inp.SrcFile, "srcFile"));
			else if (inp.XmlContent != null) _input.xml = inp.XmlContent;
			else throw new InvalidDataException("Missing input XML: you must provide the 'srcFile' path or its 'xmlContent'");

			if (inp.CertificateName != null || inp.CertificateSn != null) //Certyfikat z lokalnego magazynu?
			{
				_input.certificate = RetrieveFromStore(inp.CertificateName, inp.CertificateSn); //w tym momencie może się wyświetlić okno dialogowe z PIN-em
				if (_input.certificate == null)
					throw new KeyNotFoundException($"Did not found any valid certificate with Friendly Name = '{inp.CertificateName}' " +
																		$"in the local store of the current user ({Environment.UserName})");
			}
			else //Certyfikat z pliku/tekstu
				_input.certificate = LoadCertificate(inp.CertificateFile, inp.CertificatePem,
																					inp.PrivateKeyFile, inp.PrivateKeyPem,pwd:inp.Password??"");

			if (inp.DstFile != null) _output.SignedFile = ValidateForOutput(inp.DstFile);

			return Task.CompletedTask;
		}

		//Pomocnicza funkcja, odczytująca certyfikat z lokalnego magazynu Windows (aktualnego użytkownika)
		//Argumenty:
		//	friendlyName: "nazwa potoczna", pod którą certyfikat figuruje na liście magazynu
		//	serialNumber: numer seryjny certyfikatu z magazynu
		//UWAGA: sprawdza także terminy ważności certyfikatów, i wybiera tylko spośród aktualnie obowiązujących.
		private static X509Certificate2? RetrieveFromStore(string? friendlyName, string? serialNumber)
		{
			var store = new X509Store(StoreLocation.CurrentUser);
			store.Open(OpenFlags.ReadOnly);
			var certificates = store.Certificates;
			X509Certificate2? result = null;
			foreach (var certificate in certificates)
			{
				bool match = certificate.FriendlyName == friendlyName || certificate.SerialNumber == serialNumber;
				match &= certificate.NotAfter > DateTime.Now && certificate.NotBefore < DateTime.Now;

				if (match)
				{
					result = certificate;
					break;
				}
			}
			store.Close();
			return result;
		}

		//Podpisanie pliku za pomocą pobranego certyfikatu
		public override Task ProcessAsync(CancellationToken stopToken)
		{
			string signedXML = SignatureService.Sign(_input.xml, _input.certificate);
			signedXML ??= ""; //Na wszelki wypadek - gdy coś poszło nie tak i SignAsync zwróciło null, zwróci pusty plik
			if (_output.SignedFile != null)
				File.WriteAllText(_output.SignedFile, signedXML);
			else _output.Base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(signedXML));
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			//Na wszelki wypadek,  sprawdźmy, czy wszystko się zgadza:

			//Odczytaj to co zapisałeś:
			string xmlContent;
			if (_output.SignedFile != null)
			{
				xmlContent = File.ReadAllText(_output.SignedFile);
			}
			else 
			{
				Debug.Assert(_output.Base64Content != null);
				xmlContent = Encoding.UTF8.GetString(Convert.FromBase64String(_output.Base64Content));	
			} 

			//Sprawdź i zgłoś wyjątek, gdy coś jest nie tak:	
			if (Verify(xmlContent)) //niezależna weryfikacja podpisu, m.in. liczy ponownie hash (digest) podpisanego XML-a
				return _output.ToJson();
			else //umieszczam te throw dla porządku, bo Verify() sama zgłasza wyjątki gdy coś jest nie tak.
				throw new InvalidDataException($"Signature applied by the KSeF API 'SignatureService' is invalid."); 																				 
		}

		//Na wszelki wypadek: niezależnie sprawdzenie poprawności podpisu
		//Argument:
		//	xmlContent: zawartość (tekst) pliku XML do sprawdzenia
		public static bool Verify(string xmlContent)
		{
			try
			{
				XmlDocument xmlDocument = new()
				{
					PreserveWhitespace = true   //bez tego ustawienia programowi wyjdzie inny hash pliku (digest)
				};

				xmlDocument.LoadXml(xmlContent);
				if (xmlDocument.DocumentElement == null) throw new InvalidOperationException($"Cannot load XML data from the source content");

				SignedXml signedXml = new(xmlDocument);

				XmlNodeList nodeList = xmlDocument.GetElementsByTagName("Signature"); //Spróbuj znaleźć element <Signature>

				if (nodeList.Count <= 0) throw new CryptographicException("Cannot find 'Signature' element in this XML content.");
				//Wątpię, by kiedykolwiek to się zdarzyło, ale na wszelki wypadek:
				if (nodeList[0] is not XmlElement signature) throw new CryptographicException($"'Signature' element is empty");

				signedXml.LoadXml(signature); // informacja towarzysząca: zawartość węzła <Signature>
				
				// Sprawdź poprawność (może zgłosić wyjątek)
				return signedXml.CheckSignature(); //Sprawdza, m.in liczy ponownie hash (digest) badanego pliku XML
			}
			catch (Exception exc)
			{
				throw new Exception($"Applied signature is invalid", exc);
			}
		}
	}
}
