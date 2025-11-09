using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Zamyka sesję wsadową (inicjalizuje przetwarzanie faktur przygotowanych żądaniem OpenBatchSession).
	[HandlesRequest("CloseBatchSession")]
	internal class CloseBatchSession : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string ReferenceNumber { get; set; } //Numer referencyjny do otwartej sesji wsadowej
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}
		//----------------------
		protected InputData? _input;

		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return false; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			Debug.Assert(_input != null);
			await _ksefClient.CloseBatchSessionAsync(_input.ReferenceNumber, _input.AccessToken, stopToken);
		}

		public override string SerializeResults()
		{
			throw new NotImplementedException();
		}
	}
}
