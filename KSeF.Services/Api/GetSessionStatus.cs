using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Zwraca status sesji "wysyłkowej" (tj. sesji wysyłania faktur)
	[HandlesRequest("GetSessionStatus")]
	internal class GetSessionStatus : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string ReferenceNumber { get; set; } //numer zwrócony przez OpenOnlineSessionInvoice

			public string? SaveUpoAs { get; set; } //Opcjonalny: wpisz tu ścieżkę pliku, w którym chcesz mieć zapisane
												   //zbiorcze UPO faktur przesłanych w tej sesji. (Zadziała, jeżeli sesja ma status 200)
			public required string AccessToken { get; set; } //ważny token dostępowy
		}
		//UWAGA: jeżeli sesja ma więcej upo niż jeden (przy obecnych limitach to wysoce wątpliwe)
		//- to DRUGI plik "saveUpoAs" otrzyma przyrostek "-1, trzeci - "-2", itd.
		//UPO nie będą zapisane, jeżeli plik o wskazanej nazwie już istnieje.

		//	Rezultat:	verbatim z KSeF.Client (SessionStatusResponse)
		protected InputData? _input;
		protected SessionStatusResponse _output = new();
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
			_output = await _ksefClient.GetSessionStatusAsync(_input.ReferenceNumber, _input.AccessToken, stopToken);

			//Czy mamy zapisać UPO?
			if (_input.SaveUpoAs != null && _output.Upo != null && _output.Upo.Pages.Count > 0 
										&& !File.Exists(_input.SaveUpoAs)) //Taki plik UPO już istnieje - nie zapisujemy powtórnie
			{

				var restClient = Scope.GetRequiredService<IRestClient>();
				var pathPrefix = Path.Combine(Path.GetDirectoryName(_input.SaveUpoAs)??"", Path.GetFileNameWithoutExtension(_input.SaveUpoAs));
				var ext = Path.GetExtension(_input.SaveUpoAs); //zapewne ".xml", ale może być inaczej

				for(int i = 0; i < _output.Upo.Pages.Count;i++)
				{
					var page = _output.Upo.Pages.ElementAt<UpoPageResponse>(i);
					string xml = await DownloadTextAsync(restClient, page.DownloadUrl, stopToken);
					if (xml != "") //Jak otrzymaliśmy jakiś wynik - to zapisz go we wskazanym miejscu na dysku
					{
						var idx = (i == 0 ? "" : $"-{i}");
						var path = $"{pathPrefix}{idx}{ext}";
						File.WriteAllText(path, xml);
						Logger.LogInformation("UPO file for session '{SessionNumber}' saved as '{Path}'", _input.ReferenceNumber, path);
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
