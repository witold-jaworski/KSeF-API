using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Zwraca listę metadanych certyfikatów z wybranego magazynu certyfikatów Windows
	[HandlesRequest("#ListStoreCertificates")]
	internal class ListStoreCertificates : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string Store {  get; set; } //Dwie wartości specjalne: "CurrentUser", "LocalMachine"
			public string OidPolicies { get; set; } = "0.4.0.194112.1.1;0.4.0.194112.1.2;0.4.0.194112.1.3"; //OID możliwych polityk certyfikatów kwalifikowanych EU
			public string SubjectPhrases { get; set; } = ""; //frazy, które powinny wystąpić w polu Subject certyfikatu
			public X509KeyUsageFlags UsedFor { get; set; } = X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation;
		}
		/*UWAGI: 
		 1.gdy "store" : "", to oznacza "CurrentUser"
		 2.gdy nie chcesz nakładać warunku na "usedFor", nadaj jej wartość "None".
		  Dostępne wartości pola (zamiast tekstu można wpisać podaną obok liczbę:
			"None" (0)
			"EncipherOnly" (1)
			"CrlSign" (2)
			"KeyCertSign" (4)
			"KeyAgreement" (8)
			"DataEncipherment" (16)
			"KeyEncipherment" (32)
			"NonRepudiation" (64)
			"DigitalSignature" (128)
			"DecipherOnly" (32768)
		Aby uzyskać złożenie kilku flag, nadaj temu polu wartość = sumie ich liczb 
		*/

		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public X509Store? store;
			public List<string> policies = [];
			public List<string> phrases = [];
			public X509KeyUsageFlags usage = X509KeyUsageFlags.None;
		}

		//----------------------
		protected Params _input = new();
		protected List<CertificateMetadata> _output = [];

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			if (inp.Store != "")
			{
				_input.store = inp.Store switch
				{
					"CurrentUser" => new X509Store(StoreLocation.CurrentUser),
					"LocalMachine" => new X509Store(StoreLocation.LocalMachine),
					_ => new X509Store(inp.Store),
				};
			}
			else
			{
				_input.store = new X509Store(StoreLocation.CurrentUser);
			}

			if (inp.OidPolicies != "")
			{
				foreach (var policy in inp.OidPolicies.Split(';'))
										_input.policies.Add(policy);
			}

			if (inp.SubjectPhrases != "")
			{
				foreach (var phrase in inp.SubjectPhrases.Split(';'))
										_input.phrases.Add(phrase);
			}

			_input.usage = inp.UsedFor;

			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input.store != null);
			_input.store.Open(OpenFlags.ReadOnly);
			var certs = _input.store.Certificates;
			X509Certificate2Collection selection = [];
			if(_input.usage != X509KeyUsageFlags.None) certs = certs.Find(X509FindType.FindByKeyUsage, _input.usage, true);

			if (_input.policies.Count == 0)
			{
				selection = certs;
			}
			else
			{
				foreach (var policy in _input.policies)
				{
					var result = certs.Find(X509FindType.FindByCertificatePolicy, policy, true);
					foreach (var cert in result) selection.Add(cert);
				}
			}
			
			foreach (var cert in selection)
			{
				if (_input.phrases.Count == 0)
				{
					_output.Add(ReadCertificateMetadata(cert));
				}
				else
				{
					foreach (var phrase in _input.phrases)
						if (cert.Subject.Contains(phrase)) _output.Add(ReadCertificateMetadata(cert));
				}
			}
			_input.store.Close();
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

	}
}
