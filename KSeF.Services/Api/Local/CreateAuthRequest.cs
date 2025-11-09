using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Tworzy wypełnioną danymi strukturę XML potrzebną do zalogowania się do KSeF
	[HandlesRequest("#CreateAuthRequest")]
	internal class CreateAuthRequest : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string Challenge { get; set; } //numer ref. z pierwszego kroku autoryzacji
			public string? Nip { get; set; } //identyfikator kontekstu
			public string? Iid { get; set; } //identyfikator kontekstu
			public string? NipVatUe { get; set; } //identyfikator kontekstu
			public bool UseFingertip { get; set; } = false;
		}
		/*
		 * UWAGA: Wymagane jest podanie jednego z możliwych identyfikatorów kontekstu: (nip | iid | nipVatUE)
		 */
		protected class Results
		{
			public string XmlContent { get; set; } = string.Empty;
		}
		//----------------------------

		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));
			_output.XmlContent = BuildAuthRequest(input.Challenge, input.Nip, input.Iid, input.NipVatUe, input.UseFingertip);
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
	}
}
