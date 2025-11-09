using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Wysłanie (interaktywne) faktury sprzedaży do KSeF
	[HandlesRequest("SendOnlineSessionInvoice")]
	internal class SendOnlineSessionInvoice : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string InvoiceFile { get; set; } //Ścieżka do pliku XML z wysyłaną fakturą (może być względna)
			public bool OfflineMode { get; set; } = false;	//True tylko dla faktur, których wizualizacje z QR II wcześniej otrzymał klient
			public string? HashOfCorrectedInvoice { get; set; } //Opcjonalny, tylko dla tzw. "korekt technicznych" trybu offline
			public required string ReferenceNumber { get; set; } //Numer referencyjny do otwartej sesji interaktywnej
			public required CipherData Encryption { get; set; } //Dane potrzebne do wysłania faktury
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string InvoiceReferenceNumber { get; set; } = ""; //Numer referencyjny do otwartej sesji
			public string InvoiceHash { get; set; } = "";	//Skrót pliku faktury, enkodowany w Base64
		}

		//Struktura danych wewnętrznych:
		protected class Params
		{
			public SendInvoiceRequest Request { get; set; } = new();
			public string ReferenceNumber { get; set; } = "";
			public string AccessToken { get; set; } = "";
		}

		//----------------------		
		protected Params _params = new();
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_params.AccessToken = inp.AccessToken;
			_params.ReferenceNumber = inp.ReferenceNumber;

			var invoice = File.ReadAllBytes(ValidateForInput(inp.InvoiceFile));
			var key = Convert.FromBase64String(inp.Encryption.Base64Key);
			var iv = Convert.FromBase64String(inp.Encryption.Base64Mix);
			var crSvc = Scope.GetRequiredService<ICryptographyService>();
			var metadata = crSvc.GetMetaData(invoice);
			var encInvoice = crSvc.EncryptBytesWithAES256(invoice, key, iv);
			var encMetadata = crSvc.GetMetaData(encInvoice);

			var builder = SendInvoiceOnlineSessionRequestBuilder.Create()
								.WithInvoiceHash(metadata.HashSHA, metadata.FileSize)
								.WithEncryptedDocumentHash(encMetadata.HashSHA, encMetadata.FileSize)
								.WithEncryptedDocumentContent(Convert.ToBase64String(encInvoice))
								.WithOfflineMode(inp.OfflineMode);

			if(inp.HashOfCorrectedInvoice != null)	builder = builder.WithHashOfCorrectedInvoice(inp.HashOfCorrectedInvoice);

			_params.Request = builder.Build();

			_output.InvoiceHash = metadata.HashSHA;

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);

			var result = await _ksefClient.SendOnlineSessionInvoiceAsync(_params.Request, _params.ReferenceNumber, _params.AccessToken, stopToken);
			if (result == null) throw new NullReferenceException("referenceNumber");
			_output.InvoiceReferenceNumber = result.ReferenceNumber;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
