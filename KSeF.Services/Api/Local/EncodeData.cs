using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#EncodeData")]
	internal class EncodeData:HandlerBase
	{
		private const int MIX_LENGTH = 16; //długość wektora "entropii"
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? Mix { get; set; } //opcjonalny: DAPI: dodatkowy kod maskujący 
			public bool NoMix { get; set; } = false; //true: nie stosuj dodatkowego maskowania w DAPI
			public string? Pwd { get; set; } //opcjonalny:	AES: hasło (zalecane kilkanaście znaków)
			public string? PlainBase64 { get; set; } //ciąg bajtów do zaszyfrowania, enkodowany w Base64
		}
		//UWAGI: zaszyfrowane dane mają taką samą długość, niezależnie od tego, czy stosujesz Mix, czy nie.

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string? Mix { get; set; } //opcjonalny: DAPI: dodatkowy kod maskujący (zachowaj do dekodowania)
			public string? Timestamp { get; set; } //znacznik czasu
			public string? EncryptedBase64 { get; set; } //zaszyfrowany ciąg bajtów, enkodoway w Base64
		}
		//UWAGA: jeżeli zapiszesz w bazie danych dwie pary:
		//	w jednym miejscu - "timestamp" i "mix"
		//	w innym miejscu - "timestamp" i "encryptedBase64"
		//to potem przed użyciem "mix" do dekodowania "encryptedBase64" możesz porównać ich "timestamp",  
		//aby się upewnić, że pochodzą z tej samej operacji i będą do siebie pasować.

		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public byte[] mix = [];		  //wektor "entropii" dla DAPI (długość dowolna)
			public byte[] key = [];		  //klucz dla AES256 (32 bajty)
			public byte[] iv = [];		  //iv dla AES256 (16 bajtów)
			public byte[] plainData = []; //dane do zaszyfrowania
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
			if (input.PlainBase64 == null || input.PlainBase64 == "") throw new ArgumentException($"No data field - nothing to encrypt", "plainBase64");
			_params.plainData = Convert.FromBase64String(input.PlainBase64);

			if (input.Pwd != null && input.Pwd.Length > 0)
			{//AES
				(_params.key, _params.iv) = ToAes256Parameters(input.Pwd);
			}
			else
			{//DAPI
				if (input.NoMix == false) //zapisz w _params.mix wektor "entropii"
				{
					if (input.Mix != null) //Odczytaj podany mix:
					{
						if (input.Mix == "") throw new ArgumentException($"Empty mix field - cannot encrypt", "mix");
						_params.mix = Convert.FromBase64String(input.Mix);
					}
					else //Wygeneruj losowy mix:
					{
						_params.mix = new byte[MIX_LENGTH];
						var rng = RandomNumberGenerator.Create();
						rng.GetBytes(_params.mix);
					}
				}
			}
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			byte[] bytes;

			if (_params.key.Length == 32)
			{//AES
				var cryptoService = Scope.GetRequiredService<ICryptographyService>();
				bytes = cryptoService.EncryptBytesWithAES256(_params.plainData, _params.key, _params.iv);
			}
			else
			{//DAPI
				if (_params.mix.Length == 0) //bez dodatkowego "zaciemniania"
				{
					bytes = ProtectedData.Protect(_params.plainData, null, DataProtectionScope.CurrentUser);
				}
				else //z dodatkowym "zaciemnianiem"
				{
					bytes = ProtectedData.Protect(_params.plainData, _params.mix, DataProtectionScope.CurrentUser);
					_output.Timestamp = DateTime.UtcNow.GetTimestamp();
					_output.Mix = Convert.ToBase64String(_params.mix);
				}
			}
			_output.EncryptedBase64 = Convert.ToBase64String(bytes);
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
