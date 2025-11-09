using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;
using KSeF.Client.Core.Models;

namespace KSeF.Services.Api
{
	//Zwraca status zadania autoryzacji. Wywoływać po SubmitXadesAuthRequest
	[HandlesRequest("GetAuthStatus")]
	internal class GetAuthStatus : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string ReferenceNumber { get; set; } //Zwrócone przez SubmitXadesAuthRequest
			public required string AuthToken { get; set; } //token z AuthenticationToken, zwróconej przez SubmitXadesAuthRequest
		}
		//	Rezultat:	verbatim z KSeF.Client (AuthStatus)
		protected InputData? _input;
		protected AuthStatus? _output;

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
			_output = await _ksefClient.GetAuthStatusAsync(_input.ReferenceNumber, _input.AuthToken, stopToken);
		}

		public override string SerializeResults()
		{
			Debug.Assert(_output != null);
			return _output.ToJson();
		}
	}
}
