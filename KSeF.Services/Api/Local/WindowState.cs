using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#WindowState")]
	internal class WindowState : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required int SetTo { get; set; }   //nowy stan okna (na jedną ze stałych ConsoleWindow.SW_*), lub -1, gdy "tylko pytam"
		}
		//UWAGI: "byteArray" i "text" to alternatywy: można podać albo jedno, albo drugie, ale nie oba naraz.

		//Struktura danych wyjściowych:
		protected class Results
		{
			public int Before { get; set; } = -1; //stan okna przed tym wywołaniem
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
			_output.Before = ConsoleWindow.GetActualState();
			if (_input  != null && _input.SetTo >= 0 && _input.SetTo <= 10) //0 = SW_HIDE, 10 = SW_SHOWDEFAULT
			{
				ConsoleWindow.SetState(_input.SetTo);
			}
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
