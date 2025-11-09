using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Sessions;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#GetEncryptionData")]
	//Zwraca nowy (losowy) klucz symetryczny: w oryginale (do zastosowania)
	//i zaszyfrowany kluczem publicznym (do przekazania do KSeF)
	internal class GetEncryptionData : HandlerBase
	{
		//------------ Struktury ------------------
		protected EncryptionData _output = new();

		public override bool RequiresInput { get { return false; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			throw new NotImplementedException();
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			_output = cryptoService.GetEncryptionData();
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
