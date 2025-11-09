using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KSeF.Services.Test
{
	//Przykładowa implementacja tworzenia/zmiany/usuwania elementu
	[HandlesRequest("/posts")]
	internal class PostHandler: DefaultHandler, IRequestHandler
	{
		protected HttpMethod _method = HttpMethod.Connect;	//coś trzeba wpisać, aby nie było błędu - wpisuję metode, której na pewno nie użyję
		protected ExpandoObject? _data = null;				//Odczytane dane

		//Assign - obsługa z klasy bazowej
		public new bool RequiresInput { get { return true; } }

		//Określa dane i metodę
		public new Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			IDictionary<string, object?>? inp = JsonExtensions.Parse(data) as IDictionary<string, object?>; //Do sprawdzania, czy dane pole istnieje:
			Debug.Assert(inp != null);	
			_method = new HttpMethod(inp.AsString("method")); //trzeba je poddawać jawnej konwersji
			if (inp.ContainsKey("id")) _request += $"/{inp["id"]}";
			if (inp.ContainsKey("data")) _data = inp["data"] as ExpandoObject;
			return Task.CompletedTask;
		}

		public new async Task ProcessAsync(CancellationToken stopToken)
		{
			if(_restClient == null) return;

			_response = await _restClient.SendAsync<string, ExpandoObject?>(_method,
																			_request,
																			_data,
																			cancellationToken: stopToken).ConfigureAwait(false);
			return;
		}

	}

}
