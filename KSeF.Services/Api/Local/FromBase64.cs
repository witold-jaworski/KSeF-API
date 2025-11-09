using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#FromBase64")]
	internal class FromBase64 : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public bool AsBytes { get; set; } = false; //czy rezultat ma być zwrócony jako tablica bajtów?
			public string? Base64 { get; set; } //bajty tablicy enkodowane w Base64
		}
		//UWAGI: asBytes jest domyślnie false, bo w większości przypadków będę enkodował teksty

		//Struktura danych wyjściowych:
		protected class Results
		{
			public int [] ? ByteArray { get; set; } //lista liczb (wartości bajtów) 
			public string ? Text { get; set; }	//zdekodowany tekst
		}
		//Uwaga:w tej tablicy nie mogę użyć typu byte, bo wtedy JsonUtil automatycznie zserializuje ją na Base64

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
			if (_input != null && _input.Base64 != null) //dla porządku, aby VS nie zgłaszano uwag
			{
				byte[] bytes;
				bytes = Convert.FromBase64String(_input.Base64);

				if (_input.AsBytes)
					_output.ByteArray = bytes.Select(x => (int)x).ToArray();
				else //zwróć jako tekst
					_output.Text = Encoding.UTF8.GetString(bytes);
			}
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
