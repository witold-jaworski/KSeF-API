using KSeF.Client.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#ToBase64")]
	internal class ToBase64 : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public List<byte> ByteArray { get; set; } = []; //lista liczb (wartości bajtów)
			public string? Text { get; set; }	//tekst 
		}
		//UWAGI: "byteArray" i "text" to alternatywy: można podać albo jedno, albo drugie, ale nie oba naraz.

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string? Base64 { get; set; } //bajty tablicy / tekstu enkodowane w Base64
		}
		//UWAGI: jeżeli w danych wejściowych  podano tekst, to są jego bajty (UTF-8), enkodowane w Base64

		//----------------------
		protected InputData? _input;
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			if (_input != null) //dla porządku, aby VS nie zgłaszano uwag
			{
				var bytes = _input.ByteArray.ToArray() as byte[];
				if (bytes.Length == 0) //Tablica pusta?
				{ //Sprawdź , czy podano tekst:
					if (_input.Text == null) throw new Exception($"Empty input data - expected 'byteArray' or 'text'");
					bytes = Encoding.UTF8.GetBytes(_input.Text); //przekształć na bajty UTF-8
				}
				_output.Base64 = Convert.ToBase64String(bytes);
			}
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
