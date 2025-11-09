using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Models.Authorization;

namespace KSeF.Services.Api
{
	[HandlesRequest("GetAuthChallenge")]
	internal class GetAuthChallenge : HandlerBase
	{
		//------------ Struktury ------------
		//		verbatim z KSeF.Client
		//-----------------------------------
		protected AuthenticationChallengeResponse _output = new();
		public override bool RequiresInput { get { return false; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			throw new NotImplementedException();
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			_output = await _ksefClient.GetAuthChallengeAsync(stopToken);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
