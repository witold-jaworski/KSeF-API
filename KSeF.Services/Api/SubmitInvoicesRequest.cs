using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions.OnlineSession;
using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services.Api
{
	//Wysyła żądanie paczki faktur spełniających podane kryteria 
	[HandlesRequest("SubmitInvoicesRequest")]
	internal class SubmitInvoicesRequest : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required InvoiceQueryFilters Filters { get; set; } //wybór faktur (taka sama struktura, jak w ListSubjectInvoices)
			public required string AccessToken { get; set; } //ważny token dostępowy
		}

		//Struktura danych wyjściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class Results
		{
			public string ReferenceNumber { get; set; } = ""; //referencja do żądania pobrania faktur
			public CipherData Encryption { get; set; } = new(); //Dane potrzebne do odszyforwania paczek faktur
		}

		//Struktura danych wewnętrznych:
		protected class Params
		{
			public InvoiceExportRequest Request { get; set; } = new();
			public string AccessToken { get; set; } = "";
		}

		protected Params _params = new();
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_params.AccessToken = input.AccessToken;
			_params.Request.Filters = input.Filters;

			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			var enc = cryptoService.GetEncryptionData();

			_params.Request.Encryption = enc.EncryptionInfo;
			_output.Encryption.Base64Key = Convert.ToBase64String(enc.CipherKey);
			_output.Encryption.Base64Mix = Convert.ToBase64String(enc.CipherIv);

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			var result = await _ksefClient.ExportInvoicesAsync(_params.Request, _params.AccessToken, stopToken);
			_output.ReferenceNumber = result.ReferenceNumber;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
