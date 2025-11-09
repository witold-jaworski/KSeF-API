using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Zwraca metadane podanego certyfikatu
	[HandlesRequest("#GetCertificateProperties")]
	internal class GetCertificateProperties : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? SignedXml { get; set; }  //treść podpisanego XML (string)
			public string? SignedXmlFile { get; set; }  //ścieżka do podpisanego pliku
			public string? CertificateFile { get; set; } //ścieżka do pliku certyfikatu (*.pem)
			public string? CertificatePem { get; set; } //tekst certyfikatu PEM ("-----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----")
			public string? PrivateKeyFile { get; set; } //ścieżka do pliku z kluczem prywatnym (*.pem)
			public string? PrivateKeyPem { get; set; } //tekst certyfikatu PEM ("-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----")
			public string? Password { get; set; }	   //ewentualne hasło do klucza
		}
		//W tej strukturze należy wypełnić JEDNO z jej pól
		X509Certificate2? _cert; //Certyfikat do przeanalizowania
		CertificateMetadata? _output;

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (inp.CertificateFile != null) _cert = LoadCertificate(inp.CertificateFile, null, inp.PrivateKeyFile, inp.PrivateKeyPem, true, inp.Password ?? "");
			if (inp.CertificatePem != null) _cert = LoadCertificate(null,inp.CertificatePem, inp.PrivateKeyFile, inp.PrivateKeyPem, true, inp.Password ?? "");
			if (inp.SignedXmlFile != null) 	inp.SignedXml = File.ReadAllText(ValidateForInput(inp.SignedXmlFile, "signedXmlFile"));
			if (inp.SignedXml != null)
			{
				var xmlDoc = new XmlDocument() 	{ PreserveWhitespace = true };
				xmlDoc.LoadXml(inp.SignedXml);
				var nspaces = new XmlNamespaceManager(xmlDoc.NameTable);
				nspaces.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
				nspaces.AddNamespace("ppZP", "http://crd.gov.pl/xml/schematy/ppzp/");               //To ns jest związane z ePUAP
				var certificates = xmlDoc.SelectNodes("//ds:X509Certificate", nspaces);
				if (certificates != null && certificates.Count > 0)
				{
					var node = certificates[0]; //zmienna dodana tylko po to, by CA owaliło się ze swoim głupim ostrzeżeniem CS8602
					if (node != null) _cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(node.InnerText));
					node = xmlDoc.SelectSingleNode("//ppZP:PodpisZP", nspaces); //Obecność tego węzła jest znacznikiem popisu przez profil zaufany
					if (_cert != null && node != null) //Imię i nazwisko w ePUAP trzeba wyciągnąć z XML-a:
					{
						var firstName = node.SelectSingleNode(".//*[local-name()='Imie']"); //Nie używam ns "os:", bo może kiedyś sie zmieni...
						var surname = node.SelectSingleNode(".//*[local-name()='Nazwisko']");

						if (firstName != null && surname != null)
						{
							_cert.FriendlyName = $"{firstName.InnerText} {surname.InnerText}";
						}
					}
				}
				else
				{
					if (inp.SignedXmlFile != null) 
						throw new InvalidDataException($"File '{inp.SignedXmlFile}' does not contain any signature");
					else 
						throw new InvalidDataException($"Provided XML does not contain any signature");
				}

			}
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_cert != null);
			_output = ReadCertificateMetadata(_cert);
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
