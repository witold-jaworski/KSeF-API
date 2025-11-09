using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;
using KSeF.Client.Core.Models.Authorization;

namespace KSeF.Services.Api
{
	//Zwraca tokeny dostępowe ("refresh" i "access"). Może być wywołana tylko raz, kończy sekwencję uwierzytelniania
	//rozpoczętą przez GetAuthChallenge. Wywoływać, gdy kod statusu sesji uwierzytelnienia zmienił się na 200.
	//UWAGA: tokeny dostępowe sesji uwierzytelnienia można pobrać tylko raz!
	[HandlesRequest("GetAccessTokens")]
	internal class GetAccessTokens : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string AuthToken { get; set; } //token z AuthenticationToken, zwróconej przez SubmitXadesAuthRequest
		}
		//	Rezultat:	verbatim z KSeF.Client (AuthOperationStatusResponse)
		protected InputData? _input;
		protected AuthenticationOperationStatusResponse? _output;
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
			_output = await _ksefClient.GetAccessTokenAsync(_input.AuthToken, stopToken);
		}

		public override string SerializeResults()
		{
			Debug.Assert(_output != null);
			return _output.ToJson();
		}
	}
}
