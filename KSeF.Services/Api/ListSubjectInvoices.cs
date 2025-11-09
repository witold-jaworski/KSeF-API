using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;
using KSeF.Client.Core.Models.Invoices;

namespace KSeF.Services.Api
{
	//Zwraca listę (lub jej część) faktur w których aktualny kontekst występuje jako jedna ze Stron (Podmiot, czyli "Subject") 
	[HandlesRequest("ListSubjectInvoices")]
	internal class ListSubjectInvoices : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required InvoiceQueryFilters Filters { get; set; } //warunki selekcji
			public int PageOffset { get; set; } = 0;       //zwiększ w kolejnym wywołaniu, jeżeli w rezultacie poprzedniego "hasMore" jest true;
			public int PageSize { get; set; } = 250;       //max. liczba elementów w zwracanej liście faktur (min.: 10)
			public required string AccessToken { get; set; } //ważny token dostępowy
		}
		//	Rezultat:	verbatim z KSeF.Client (PagedInvoiceResponse)
		protected InputData? _input;
		protected PagedInvoiceResponse _output = new(); //Rezultat: lista metadanych faktur

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

			_output = await _ksefClient.QueryInvoiceMetadataAsync(_input.Filters, _input.AccessToken, _input.PageOffset, _input.PageSize,
																													cancellationToken:stopToken);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
