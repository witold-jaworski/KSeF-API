using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#ValidateKsefNumber")]
	internal class ValidateKsefNumber : HandlerBase
	{
		//Symbole możliwych rezultatów porównania:
		protected enum CheckResults
		{
			Ok,
			EmptyString,
			WrongLength,
			BadCrc
		}
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public string? NumberKsef { get; set; } //numer KSeF do sprawdzenia
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public CheckResults Result { get; set; } = CheckResults.Ok; //rezultat weryfikacji
			public string? Crc { get; set; } //poprawna suma kontrolna
		}
		//UWAGA: crc jest zwracane, gdy result = BadCrc

		//----------------------
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			if (input.NumberKsef == null) throw new ArgumentException($"No data field - nothing to check", "numberKsef");
			_output.Result = IsValid(input.NumberKsef, out string? crc);
			_output.Crc = crc;
			return Task.CompletedTask;
		}

		public override Task ProcessAsync(CancellationToken stopToken)
		{
			return Task.CompletedTask;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

		//--- Walidacja numeru KSeF (na podstawie KSeF.Client.Core.KsefNumberValidator) ---
		//Stałe do obliczania sumy kontrolnej
		private const byte Polynomial = 0x07;
		private const byte InitValue = 0x00;

		//Stałe do walidacji:
		private const int ExpectedLength = 35;
		private const int DataLength = 32;
		private const int ChecksumLength = 2;

		//Walidacja numeru KSeF 
		//Argumenty:
		//	ksefNumber: numer KSeF
		//	crc:		wyznaczona suma kontrolna (gdy rezultatem funkcji jest BadCrc)
		//Zwraca wartość enumeracji z wynikiem porównania
		protected static CheckResults IsValid(string ksefNumber, out string? crc)
		{
			crc = null;

			if (string.IsNullOrWhiteSpace(ksefNumber)) return CheckResults.EmptyString;

			if (ksefNumber.Length != ExpectedLength) return CheckResults.WrongLength;

			string data = ksefNumber[..DataLength];
			string checksum = ksefNumber[^ChecksumLength..];

			crc = ComputeChecksum(Encoding.UTF8.GetBytes(data));

			if(crc != checksum) return CheckResults.BadCrc;

			crc = null; //sumy kontrolne są zgodne, więc jej nie zwracaj

			return CheckResults.Ok;
		}

		//Oblicza sumę kontrolną
		//Argumenty:
		//	data: wartości kodów ASCII pierwszych 32 znaków numeru KSeF (razem z rozdzielającymi myślnikami)
		private static string ComputeChecksum(byte[] data)
		{
			byte crc = InitValue;

			foreach (byte b in data)
			{
				crc ^= b;
				for (int i = 0; i < 8; i++)
				{
					crc = (crc & 0x80) != 0
						? (byte)((crc << 1) ^ Polynomial)
						: (byte)(crc << 1);
				}
			}

			return crc.ToString("X2"); // always 2-char hex
		}
	}
}
