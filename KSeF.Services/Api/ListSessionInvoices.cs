using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Zwraca listę (lub jej część) faktur wysłanych w sesji ("wysyłkowej").
	[HandlesRequest("ListSessionInvoices")]
	internal class ListSessionInvoices : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string ReferenceNumber { get; set; } //numer sesji, zwrócony przez OpenOnlineSessionInvoice
			public int PageSize { get; set; } = 100;	   //max. liczba elementów w zwracanej liście faktur (min.: 10)
			public string? ContinuationToken { get; set; } //przy kolejnych wywołaniach: ewentualny token kontynuacji
			public string? SaveUpoTo { get; set; } //Opcjonalny: wpisz tu ścieżkę do folderu, w którym chcesz mieć zapisane
												   //indywidualne UPO faktur przesłanych w tej sesji.
												   //UWAGA: Zadziała tylko dla sesji *wsadowych*  o statusie 200
			public required string AccessToken { get; set; } //ważny token dostępowy
		}

		//	Rezultat:	verbatim z KSeF.Client (SessionInvoicesResponse)
		protected InputData? _input;
		protected SessionInvoicesResponse _output = new(); //Rezultat: lista metadanych faktur

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (_input.SaveUpoTo != null) _input.SaveUpoTo = ValidateForOutput(_input.SaveUpoTo, "saveUpoTo");

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);

			_output = await _ksefClient.GetSessionInvoicesAsync(_input.ReferenceNumber, _input.AccessToken, _input.PageSize, 
																							_input.ContinuationToken, stopToken);
			//Czy mamy zapisac indywidualne UPO faktur?
			if (_output != null && _output.Invoices != null && _output.Invoices.Count > 0 && _input.SaveUpoTo != null)
			{
				var restClient = Scope.GetRequiredService<IRestClient>();
				foreach (var invoice in _output.Invoices)
				{
					if (invoice.InvoiceFileName != null && invoice.UpoDownloadUrl != null) //invoiceFileName jest zwracany tylko dla wysyłek wsadowych
					{
						string xml = await DownloadTextAsync(restClient, invoice.UpoDownloadUrl, stopToken);
						if (xml != "") //Jak otrzymaliśmy jakiś wynik - to zapisz go we wskazanym miejscu na dysku
						{
							var path = $"{Path.Combine(_input.SaveUpoTo, Path.GetFileNameWithoutExtension(invoice.InvoiceFileName))}.upo.xml";
							if (!File.Exists(path))
							{
								File.WriteAllText(path, xml);
								Logger.LogInformation("UPO file for invoice '{InvoiceNumber}' saved as '{Path}'", invoice.InvoiceNumber, path);
							}
						}
					}
				}
			}
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

	}
}
