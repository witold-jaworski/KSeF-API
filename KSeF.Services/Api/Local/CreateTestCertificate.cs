using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Models.Authorization;

namespace KSeF.Services.Api.Local
{
	//klasa bazowa dla CreateTestSignature i CreateTestSeal
	//Tworzy certyfikaty "self-signed", przydatne w środowisku TEST
	//Klasy potomne musza zaimplementować PrepareInputAsync(), która wypełnia strukturę Params.
	internal abstract class CreateTestCertificate : HandlerBase
	{
		//Klasy potomne ustalą swoje struktury danych wejściowych

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string CertFile { get; set; } = string.Empty; //ścieżka do pliku z certyfikatem (tak - dla potwierdzenia)
		}

		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public string subjectName = string.Empty; //Kwalifikowany tekst, zgodnie z formatem certyfikatu
			public DateTime utcFrom = DateTime.MinValue;
			public DateTime utcTo = DateTime.MaxValue;
			public string saveTo = string.Empty;
			//przed wywołaniem ProcessAsync jedno z tych dwóch pól musi być wypełnione:
			public RSA? rsaKey = null;
			public ECDsa? ecdKey = null;
		}

		//----------------------
		protected Params _input = new();
		protected Results _output = new();

		//pomocnicza funkcja: nie konwertuje na UTC czasu przekazanego jako sama data
		//(Aby nie zaskakiwać użytkownika)
		private static DateTime TimeToUtc(DateTime local)
		{
			if (local.TimeOfDay.TotalMilliseconds > 0) 
				return local.ToUniversalTime();
			else
				return local;
		}

		//Procedura dla klas potomnych, wypełniająca powtarzające się pola struktury _input
		protected void AcceptCommonParameters(EncryptionMethodEnum keyType, DateTime validFrom, DateTime validTo, string saveTo)
		{
			_input.saveTo = Program.FullPath(saveTo);
			_input.utcFrom = TimeToUtc(validFrom);
			_input.utcTo = TimeToUtc(validTo);
			switch (keyType)
			{
				case EncryptionMethodEnum.Rsa:
					_input.rsaKey = RSA.Create(2048);
					break;
				case EncryptionMethodEnum.ECDsa:
					_input.ecdKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
					break;
			}
		}
		//----- Implementacja interfejsu ----
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults  { get { return true;} }

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			X509Certificate2 cert;
			string pemKey;
			if(_input.ecdKey != null)
			{
				cert = new CertificateRequest(_input.subjectName, _input.ecdKey, HashAlgorithmName.SHA256)
																.CreateSelfSigned(_input.utcFrom, _input.utcTo);
				//pemKey = new string(PemEncoding.Write("EC PRIVATE KEY", _input.ecdKey.ExportECPrivateKey()));
				pemKey = _input.ecdKey.ExportPkcs8PrivateKeyPem(); //W formacie PKCS8 może być ujęty w zwykły "---PRIVATE KEY---
			}
			else 
			{
				Debug.Assert(_input.rsaKey != null); //Mam nadzieję, że klasy potomne staną na wysokości zadania...
				cert = new CertificateRequest(_input.subjectName, _input.rsaKey, 
													HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
																.CreateSelfSigned(_input.utcFrom, _input.utcTo);
				pemKey = _input.rsaKey.ExportPkcs8PrivateKeyPem();//.ExportRSAPrivateKeyPem();
			}
			Debug.Assert(cert != null);

			//Format zapisu dobieramy na podstawie rozszerzenia pliku podanego przez użytkownika:
			var ext = Path.GetExtension(_input.saveTo).ToLower(); 

			switch (ext)
			{
				case ".pem":
					File.WriteAllText(_input.saveTo, cert.ExportCertificatePem() + '\n' + pemKey); //znaki nowej linii w obydwu stringach to "\n"
					break;
				case ".pfx":
					File.WriteAllBytes(_input.saveTo, cert.Export(X509ContentType.Pfx)); //Hasło do klucza: puste
					break;
				default:
					//Wymuszamy zapisanie w formacie PEM
					_input.saveTo = Path.Combine(Path.GetDirectoryName(_input.saveTo)??"", 
														Path.GetFileNameWithoutExtension(_input.saveTo) + ".pem");
					File.WriteAllText(_input.saveTo, cert.ExportCertificatePem() + '\n' + pemKey);
					break;
			}
			_output.CertFile = _input.saveTo;
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
