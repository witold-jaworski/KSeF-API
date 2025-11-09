using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	//Tworzy testową pieczęć firmową, do sprawdzania w środowisku TEST
	[HandlesRequest("#CreateTestSeal")]
	internal class CreateTestSeal : CreateTestCertificate
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string FormalName { get; set; } //np. Test sp. z o.o.
			public required string Nip { get; set; } //nr NIP, np. 1111111111
			public string CountryCode { get; set; } = "PL"; 
			public string? CommonName { get; set; }
			public EncryptionMethodEnum Encryption { get; set; } = EncryptionMethodEnum.Rsa;
			public DateTime ValidFrom { get; set; } = DateTime.Now;
			public DateTime ValidTo { get; set; } = DateTime.Now.AddYears(1);
			public required string SaveTo { get; set; }   //ścieżka, w której ma być zapisany wynikowy plik certyfikatu (wraz kluczem prywatnym).
		}
		/* Uwagi:
			1. Nip musi być polski, nawet jeżeli kraj przedsiębiorstwa jest inny niż Polska (są takie przypadki).
			2. Jeżeli CommonName nie została podana, jest w nią wpisywane FormalName
			3. W encryption można podać albo "Rsa" albo "ECDsa"
			1. Rozszerzenie pliku "saveTo" określa żądany format certyfikatu. Dopuszczalne są tylko dwa: ".pem" i ".pfx"
		*/

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			//Najpierw wypełnijmy strukturę _input polami "technicznymi":
			AcceptCommonParameters(inp.Encryption, inp.ValidFrom, inp.ValidTo, inp.SaveTo);

			//Potem zbudujmy opis certyfikowanego podmiotu:
			List<string> subject = [];

			subject.Add($"2.5.4.10={inp.FormalName}");
			subject.Add($"2.5.4.97=VATPL-{inp.Nip}");
			subject.Add($"2.5.4.3={inp.CommonName??inp.FormalName}");
			subject.Add($"2.5.4.6={inp.CountryCode}");

			_input.subjectName = string.Join(", ", subject);

			return Task.CompletedTask;
		}
	}
}
