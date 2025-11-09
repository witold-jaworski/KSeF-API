using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Konwersja certyfikatu na niezaszyfrowany PEM z kluczem prywatnym w formacie PKCS#8
	[HandlesRequest("#CertificateToPem")]
	internal class CertificateToPem : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? CertificateFile { get; set; } //ścieżka do pliku certyfikatu (*.pem)
			public string? CertificatePem { get; set; } //tekst certyfikatu PEM ("-----BEGIN CERTIFICATE----- ... -----END CERTIFICATE-----")
			public string? PrivateKeyFile { get; set; } //ścieżka do pliku z kluczem prywatnym (*.pem)
			public string? PrivateKeyPem { get; set; } //tekst certyfikatu PEM ("-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----")
			public string? Password { get; set; }      //ewentualne hasło do klucza prywatnego
		}
		/* Uwagi:
			1.	Certyfikaty i klucz prywatny w formacie PEM mogą być podane wprost, jako tekst (para CertificatePem + PrivateKeyPem)
				lub poprzez wskazanie zawierających je plików (para CertificateFile + PrivateKeyFile).
			2.	Jeżeli "privateKeyPem/File" jest pominięty, program zakłada, że klucz prywatny znajduje się także w tekście "certificatePem/File"
			3.	Należy podać wartość "certificatePem/File"[+"privateKeyPem/File"] LUB "certificateName|certificateSn". To alternatywa. 
				Brak jakiejkolwiek informacji o certyfikacie wywoła wyjątek. 
		*/
		//Struktura danych wyjściowych:
		protected class Results
		{
			public string CertificatePem { get; set; } = ""; //kompletny, niezaszyfrowany certyfikat w formacie PEM
		}

		//----------------------
		protected InputData _input = new();
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (_input.CertificateFile != null) _input.CertificateFile = ValidateForInput(_input.CertificateFile, "certificateFile");
			if (_input.PrivateKeyFile != null) _input.PrivateKeyFile = ValidateForInput(_input.PrivateKeyFile, "privateKeyFile");
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			X509Certificate2? cert;
			//Format zapisu dobieramy na podstawie rozszerzenia pliku podanego przez użytkownika:
			if (_input.CertificateFile != null)
			{
				var ext = Path.GetExtension(_input.CertificateFile).ToLower();

				cert = ext switch
				{
					".pem" or ".crt" => base.LoadCertificate(_input.CertificateFile, null,
																		_input.PrivateKeyFile, _input.PrivateKeyPem, pwd: _input.Password ?? ""),
					 ".p12" or ".pfx" => X509CertificateLoader.LoadPkcs12FromFile(_input.CertificateFile,_input.Password, 
																							   X509KeyStorageFlags.Exportable),  //flaga konieczna do wydobycia klucza prywatnego!
					_ => throw new ArgumentException($"Cannot load certificate from a '*{ext}' file", "certificateFile"),
				};
			}
			else
			{
				cert = base.LoadCertificate(null, _input.CertificatePem,
																   _input.PrivateKeyFile, _input.PrivateKeyPem, pwd: _input.Password ?? "");
			}

			if (cert != null) //w przeciwnym razie handler zwróci pusty string
			{
				string? pemKey = null;
				/* .. klucz prywatny z certyfikatu stworzonego z pfx/p12 nie obsługuje ŻADNEGO eksportu! W szczególności tych poniżej: */
				if (cert.GetECDsaPublicKey() != null)	pemKey = cert.GetECDsaPrivateKey()?.ExportPkcs8PrivateKeyPem();
				if (cert.GetRSAPublicKey() != null) pemKey = cert.GetRSAPrivateKey()?.ExportPkcs8PrivateKeyPem();
				_output.CertificatePem = cert.ExportCertificatePem();
				if (pemKey != null) _output.CertificatePem += '\n' + pemKey;
			}
			
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

	}
}
