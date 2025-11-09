using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#GetMetadata")]
	internal class GetMetadata : HandlerBase
	{
		//------------ Struktury ------------------

		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? FilePath { get; set; } //wyznacz metadane tego pliku
			public string? Base64 { get; set; } //wyznacz metdadane tego ciagu bajtów enkodowanego w Base64
		}
		//UWAGI: "filePath" i "base64" to alternatywy

		//Rezultat jest zwracany w strukturze KSeF.Client...FileMetadata
		//----------------------
		protected byte[] _input = [];
		protected FileMetadata _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_input = GetBytes(input.Base64, input.FilePath, "filePath");

			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			_output = cryptoService.GetMetaData(_input);
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

	}
}
