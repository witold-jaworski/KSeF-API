using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Pobiera i zapisuje na dysku plik UPO, związany z fakturą przesłaną do KSeF
	[HandlesRequest("GetSessionInvoiceUpo")]
	internal class GetSessionInvoiceUpo : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData //Używana także do pobrania UPO
		{
			public required string ReferenceNumber { get; set; } //numer sesji, zwrócony przez OpenOnlineSessionInvoice
			public required string InvoiceReferenceNumber { get; set; } //numer ref. faktury, zwrócony przez SendOnlineSessionInvoice
			public required string DstFile { get; set; } //ścieżka, do której ma być zapisany pobrany plik UPO (może być względna)
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}
		//Struktura danych wyjściowych
		protected class Results
		{
			public string UpoFile { get; set; } = ""; //Pełna ścieżka do zapisanego pliku UPO (odpowiada DstFile)
		}
		//----------------------------------------
		//Pomocnicze zmienne
		protected InputData? _input;
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			_output.UpoFile = ValidateForOutput(_input.DstFile, "dstFile");
			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);
			var result = await _ksefClient.GetSessionInvoiceUpoByReferenceNumberAsync(_input.ReferenceNumber, _input.InvoiceReferenceNumber, _input.AccessToken, stopToken);
			File.WriteAllText(_output.UpoFile, result);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
