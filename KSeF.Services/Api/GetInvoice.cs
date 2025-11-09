using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Pobiera i zapisuje na dysku plik faktury o wskazanym numerze KSeF
	//UWAGA: aktualny kontekst musi występować na niej w którymkolwiek z Podmiotów
	[HandlesRequest("GetInvoice")]
	internal class GetInvoice : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData 
		{
			public required string KsefNumber { get; set; } //numer KSeF faktury
			public required string DstFile { get; set; } //ścieżka, do której ma być zapisany pobrany plik faktury (może być względna)
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}
		//Struktura danych wyjściowych
		protected class Results
		{
			public string InvoiceFile { get; set; } = ""; //Pełna ścieżka do zapisanego pliku faktury (odpowiada DstFile)
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
			_output.InvoiceFile = ValidateForOutput(_input.DstFile, "dstFile");
			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);
			var result = await _ksefClient.GetInvoiceAsync(_input.KsefNumber, _input.AccessToken, stopToken);
			File.WriteAllText(_output.InvoiceFile, result);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
