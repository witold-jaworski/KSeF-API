using System;
using System.Collections.Generic;
using System.Linq;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Otwarcie interaktywnej sesji wysyłania faktur sprzedaży
	[HandlesRequest("OpenOnlineSession")]
	internal class OpenOnlineSession : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required FormCode InvoiceFormat { get; set; } //Deklaracja formatu wysyłanych faktur
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string ReferenceNumber { get; set; } = ""; //Numer referencyjny do otwartej sesji
			public CipherData Encryption { get; set; } = new(); //Dane potrzebne do wysłania faktury
		}

		//Struktura danych wewnętrznych:
		protected class Params
		{
			public OpenOnlineSessionRequest Request { get; set; } = new();
			public string AccessToken { get; set; } = "";
		}

		//----------------------		
		protected Params _params = new ();
		protected Results _output = new ();

		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_params.AccessToken = inp.AccessToken;

			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			var enc = cryptoService.GetEncryptionData();
			
			_params.Request = OpenOnlineSessionRequestBuilder.Create()
				.WithFormCode(inp.InvoiceFormat.SystemCode, inp.InvoiceFormat.SchemaVersion, inp.InvoiceFormat.Value)
				.WithEncryption(enc.EncryptionInfo.EncryptedSymmetricKey, enc.EncryptionInfo.InitializationVector)
				.Build();
			
			_output.Encryption.Base64Key = Convert.ToBase64String(enc.CipherKey);
			_output.Encryption.Base64Mix = Convert.ToBase64String(enc.CipherIv);

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			var result = await _ksefClient.OpenOnlineSessionAsync(_params.Request, _params.AccessToken, stopToken);
			if (result == null) throw new NullReferenceException("referenceNumber");
			_output.ReferenceNumber = result.ReferenceNumber;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
