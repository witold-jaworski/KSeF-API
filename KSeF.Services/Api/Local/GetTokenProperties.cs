using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Token;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Zwraca metadane podanego tokena dostępowego
	[HandlesRequest("#GetTokenProperties")]
	internal class GetTokenProperties : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string AccessToken { get; set; }  //token dostępowy
		}

		InputData? _input;
		PersonToken? _output;
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return _output != null; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			var tokenService = Scope.GetRequiredService<IPersonTokenService>();
			Debug.Assert(tokenService != null);
			Debug.Assert(_input != null);
			_output = tokenService.MapFromJwt(_input.AccessToken);
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
