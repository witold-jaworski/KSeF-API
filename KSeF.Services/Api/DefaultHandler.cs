using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Http;


namespace KSeF.Services.Api
{
	[HandlesRequest("*")]
	internal class DefaultHandler : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string Method { get; set; } = "POST"; //Metoda HTTP dla żądania (nie wyszła mi deserializacja HttpMethod)
			public string? Body { get; set; } 
			public Dictionary<string, string>? Headers { get; set; }
			public string? AccessToken { get; set; } //aktualny token dostępowy
		}
		protected class Params
		{
			public object? body = null;				//Odczytane dane
			public HttpMethod method = HttpMethod.Post;
			public string contentType = RestClient.DefaultContentType;
		}
		protected InputData? _input = null;
		protected Params _params = new();
		protected string? _response;

		protected IRestClient? _restClient = null; 
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return (_response != null && _response.Length > 0); } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (_input.Body != null)
			{
				if (_input.Body.StartsWith('<'))
				{
					_params.contentType = RestClient.XmlContentType;
					_params.body = _input.Body;
				}
				else
				{
					_params.body = JsonExtensions.Parse(_input.Body.Replace('\'', '"'));
					if (_params.body == null) throw new ArgumentException($"Cannot parse expression '{_input.Body}'", "body");
				}
			}
			_params.method = new HttpMethod(_input.Method);
			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			_restClient = Scope.GetRequiredService<IRestClient>();
			Debug.Assert(_input != null);

			_response = await _restClient.SendAsync<string, object?>(_params.method, _request,
																_params.body, _input.AccessToken, _params.contentType,
																cancellationToken: stopToken, _input.Headers).ConfigureAwait(false);
		}

		public override string SerializeResults()
		{
			return _response??"{}"; //Ten warunek na wszelki wypadek
		}
	}
}
