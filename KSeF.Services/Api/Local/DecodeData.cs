using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#DecodeData")]
	internal class DecodeData:HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? Mix { get; set; } //opcjonalny: DAPI: dodatkowy kod maskujący (zwrócony w wynikach enkodowania)
			public string? Pwd { get; set; } //opcjonalny: AES: hasło (takie samo jak przy Encode)
			public string? EncryptedBase64 { get; set; } //ciąg bajtów do rozszyfrowania, enkodowany w Base64
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string? PlainBase64 { get; set; } //odszyfrowany ciąg bajtów, enkodoway w Base64
		}
		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public byte[] mix = [];           //wektor "entropii" dla DAPI
			public byte[] key = [];			  //klucz dla AES256 (32 bajty)
			public byte[] iv = [];			  //iv dla AES256 (16 bajtów)
			public byte[] encryptedData = []; //dane do rozszyfrowania
		}
		//----------------------
		protected Params _params = new();
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			if (input.EncryptedBase64 == null || input.EncryptedBase64 == "") throw new ArgumentException($"No data field - nothing to decrypt", "encryptedBase64");
			_params.encryptedData = Convert.FromBase64String(input.EncryptedBase64);
			if (input.Pwd != null && input.Pwd.Length > 0)
			{//AES
			  (_params.key, _params.iv) = ToAes256Parameters(input.Pwd);
			}
			else
			{//DAPI
				if (input.Mix == "") throw new ArgumentException($"Empty mix field - cannot decrypt", "mix");
				if (input.Mix != null) //zapisz w _params.mix losowy wektor "entropii"
					_params.mix = Convert.FromBase64String(input.Mix);
			}

			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			byte[] bytes;
			if (_params.key.Length == 32)
			{//AES
				var cryptoService = Scope.GetRequiredService<ICryptographyService>();
				bytes = cryptoService.DecryptBytesWithAES256(_params.encryptedData, _params.key, _params.iv);
			}
			else
			{//DAPI

				if (_params.mix.Length == 0) //bez dodatkowego "zaciemniania"
				{
					bytes = ProtectedData.Unprotect(_params.encryptedData, null, DataProtectionScope.CurrentUser);
				}
				else //enkodowanie wykonano z dodatkowym "zaciemnianiem"
				{
					bytes = ProtectedData.Unprotect(_params.encryptedData, _params.mix, DataProtectionScope.CurrentUser);
				}
			}
			_output.PlainBase64 = Convert.ToBase64String(bytes);
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
