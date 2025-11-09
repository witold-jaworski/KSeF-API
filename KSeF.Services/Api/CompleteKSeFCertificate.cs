using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Certificates;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Pobiera z KSeF certyfikat o podanym SN i łączy go z przekazanym w argumencie kluczem prywatnym
	[HandlesRequest("CompleteKSeFCertificate")]
	internal class CompleteKSeFCertificate : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string SerialNumber { get; set; } //Numer seryjny certyfikatu
			public required string Base64Key { get; set; } //Klucz prywatny, enkodowany w Base64
			public required EncryptionMethodEnum Encryption { get; set; } //Typ klucza prywatnego
			public string? SaveTo { get; set; }   //opcjonalne: ścieżka, w której ma być zapisany wynikowy plik certyfikatu (wraz kluczem prywatnym).
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}
		//Struktura danych wyjściowych:
		protected class Results
		{
			public string CertificatePem { get; set; } = string.Empty; //certyfikat wraz z kluczem prywatnym, w formacie PEM
			public string? CertFile { get; set; }  //opcjonalne: ścieżka do pliku z certyfikatem (jeżeli podano saveTo. tak - dla potwierdzenia)
		}
		//-----------------------
		protected InputData? _input;
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if(_input.SaveTo != null) _input.SaveTo = ValidateForOutput(_input.SaveTo);
			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);
			CertificateListRequest request = new() {CertificateSerialNumbers = []}; //było: = new List<string>()
			request.CertificateSerialNumbers.Add(_input.SerialNumber);

			var result = await _ksefClient.GetCertificateListAsync(request, _input.AccessToken, stopToken);
			if (result == null || result.Certificates.Count == 0) return; //coś nie tak - zwróci pusty tekst
			var der = result.Certificates.ElementAt(0); //wynik może być tylko jeden.
			if (der == null) return; //taki assert
			byte[] certBytes = Convert.FromBase64String(der.Certificate);
			byte[] pkey = Convert.FromBase64String(_input.Base64Key);
			var cert = X509CertificateLoader.LoadCertificate(certBytes); //new X509Certificate2(certBytes);
			_output.CertificatePem = _input.Encryption switch
			{
				EncryptionMethodEnum.Rsa => AddRSAKey(ref cert, pkey),
				EncryptionMethodEnum.ECDsa => AddECDsaKey(ref cert, pkey),
				_ => throw new NotImplementedException()//taki assert.
			};

			if (_input.SaveTo != null)
			{
				var ext = Path.GetExtension(_input.SaveTo).ToLower();

				switch (ext)
				{
					case ".pem":
						File.WriteAllText(_input.SaveTo, _output.CertificatePem); 
						break;
					case ".pfx":
						File.WriteAllBytes(_input.SaveTo, cert.Export(X509ContentType.Pfx)); //Hasło do klucza: puste
						break;
					default:
						//Wymuszamy zapisanie w formacie PEM
						_input.SaveTo = Path.Combine(Path.GetDirectoryName(_input.SaveTo) ?? "",
															Path.GetFileNameWithoutExtension(_input.SaveTo) + ".pem");
						File.WriteAllText(_input.SaveTo, _output.CertificatePem);
						break;
				}
				_output.CertFile = _input.SaveTo;
			}
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

		//Dodaje do certyfikatu klucz prywatny, zwraca jego postać w Pem
		//Argumenty:
		//	cert:	modyfikowany certyfikat
		//	pkey:	klucz prywatny
		private static string AddRSAKey(ref X509Certificate2 cert, byte[] pkey)
		{
			using RSA rsa = RSA.Create();
			rsa.ImportRSAPrivateKey(pkey, out _);

			cert = cert.CopyWithPrivateKey(rsa); 

			return cert.ExportCertificatePem() + '\n' + rsa.ExportPkcs8PrivateKeyPem();
		}

		//Dodaje do certyfikatu klucz prywatny, zwraca jego postać w Pem
		//Argumenty:
		//	cert:	modyfikowany certyfikat
		//	pkey:	klucz prywatny
		private static string AddECDsaKey(ref X509Certificate2 cert, byte[] pkey)
		{
			using ECDsa ecd = ECDsa.Create();
			ecd.ImportECPrivateKey(pkey, out _);

			cert = cert.CopyWithPrivateKey(ecd);
			return cert.ExportCertificatePem() + '\n' + ecd.ExportECPrivateKeyPem();
		}
	}
}
