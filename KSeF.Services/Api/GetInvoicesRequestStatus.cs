using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;
using KSeF.Client.Core.Models.Invoices;

namespace KSeF.Services.Api
{
	//Zwraca status żądania pobrania faktur (SubmitInvoicesRequest)
	[HandlesRequest("GetInvoicesRequestStatus")]
	internal class GetInvoicesRequestStatus : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string OperationReferenceNumber { get; set; } //Zwrócone przez SubmitinvoicesRequest
			public required string AccessToken { get; set; } //token z AuthenticationToken, zwróconej przez SubmitXadesAuthRequest
		}
		//	Rezultat:	verbatim z KSeF.Client (InvoiceExportStatusResponse)
		protected InputData? _input;
		protected InvoiceExportStatusResponse _output = new();
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

			_output = await _ksefClient.GetInvoiceExportStatusAsync(_input.OperationReferenceNumber, _input.AccessToken, stopToken);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
