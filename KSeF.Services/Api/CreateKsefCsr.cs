using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{


	//Tworzy klucz prywatny i żądanie certyfikatu wg danych otrzymanych z KSeF
	[HandlesRequest("CreateKsefCsr")]
	internal class CreateKsefCsr : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public EncryptionMethodEnum Encryption { get; set; } = EncryptionMethodEnum.Rsa;
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}
		//Struktura danych wyjściowych:
		protected class Results
		{
			public string Base64Csr { get; set; } = ""; //CSR do przekazania, enkodowane w Base64
			public string Base64Key { get; set; } = ""; //Klucz prywatny, enkodowany w Base64
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

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);
			var result = await _ksefClient.GetCertificateEnrollmentDataAsync(_input.AccessToken, stopToken);
			if (result != null)
			{
				var cryptoService = Scope.GetRequiredService<ICryptographyService>();
				(_output.Base64Csr, _output.Base64Key) = _input.Encryption switch
				{
					EncryptionMethodEnum.Rsa => cryptoService.GenerateCsrWithRsa(result, RSASignaturePadding.Pkcs1),
					EncryptionMethodEnum.ECDsa => cryptoService.GenerateCsrWithEcdsa(result),
					_ => throw new NotImplementedException()//taki assert.
				};
			}
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
