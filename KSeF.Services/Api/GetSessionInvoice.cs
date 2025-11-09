using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Pobiera status wysłanej faktury sprzedaży
	[HandlesRequest("GetSessionInvoice")]
	internal class GetSessionInvoice : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData 
		{
			public required string ReferenceNumber { get; set; } //numer sesji, zwrócony przez OpenOnlineSessionInvoice
			public required string InvoiceReferenceNumber { get; set; } //numer ref. faktury, zwrócony przez SendOnlineSessionInvoice
			public string? SaveUpoAs { get; set; } //Opcjonalny: wpisz tu ścieżkę pliku, w którym chcesz mieć zapisane
												   //UPO tej faktury. (Zadziała, gdy ma status 200)
			public required string AccessToken { get; set; } //ważny token dostępowy
		}

		//	Rezultat:	verbatim z KSeF.Client (SessionInvoice)
		protected InputData? _input;
		protected SessionInvoice? _output;
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (_input.SaveUpoAs != null) _input.SaveUpoAs = ValidateForOutput(_input.SaveUpoAs, "saveUpoAs");
			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			Debug.Assert(_ksefClient != null);
			_output = await _ksefClient.GetSessionInvoiceAsync(_input.ReferenceNumber, _input.InvoiceReferenceNumber, _input.AccessToken, stopToken);

			//Czy mamy zapisać UPO?
			if (_input.SaveUpoAs != null && _output != null && _output.UpoDownloadUrl != null 
											 &&	!File.Exists(_input.SaveUpoAs)) //Gdy taki plik UPO już istnieje - nie zapisujemy powtórnie
			{
				var restClient = Scope.GetRequiredService<IRestClient>();
				string xml = await DownloadTextAsync(restClient, _output.UpoDownloadUrl, stopToken);
				//Jak otrzymaliśmy jakiś wynik - to zapisz go we wskazanym miejscu na dysku:
				if (xml != "")
				{
					File.WriteAllText(_input.SaveUpoAs, xml);
					Logger.LogInformation("UPO file for invoice '{InvoiceNumber}' saved as '{Path}'", _output.InvoiceNumber, _input.SaveUpoAs);
				}
			}
		}

		public override string SerializeResults()
		{
			Debug.Assert(_output != null);
			return _output.ToJson();
		}
	}
}
