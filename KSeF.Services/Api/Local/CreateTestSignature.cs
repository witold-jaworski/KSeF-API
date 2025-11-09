using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#CreateTestSignature")]
	internal class CreateTestSignature : CreateTestCertificate
	{
		//Tworzy testowy podpis osobisty, do sprawdzania w środowisku TEST
		//
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string Name { get; set; } //np. Jan
			public required string Surname { get; set; } //np. Kowalski
			public string? CommonName { get; set; } //domyślnie: "{Name} {Surname}"
			public string? Pesel { get; set; } //nr NIP, np. 1998060701111
			public string? Nip { get; set; } //nr NIP, np. 1111111111
			public string CountryCode { get; set; } = "PL";
			public EncryptionMethodEnum Encryption { get; set; } = EncryptionMethodEnum.Rsa;
			public DateTime ValidFrom { get; set; } = DateTime.Now;
			public DateTime ValidTo { get; set; } = DateTime.Now.AddYears(1);
			public required string SaveTo { get; set; }   //ścieżka, w której ma być zapisany wynikowy plik certyfikatu (wraz kluczem prywatnym).
		}
		/* Uwagi:
			1. Należy podać ALBO nip, albo pesel
			2. Jeżeli CommonName nie została podana, jest w nią wpisywane "Name" "Surname" 
			3. W encryptin można podać albo "Rsa" albo "ECDsa"
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

			subject.Add($"2.5.4.42={inp.Name}");
			subject.Add($"2.5.4.4={inp.Surname}");
			subject.Add($"2.5.4.3={inp.CommonName ??$"{inp.Name} {inp.Surname}"}");
			subject.Add($"2.5.4.6={inp.CountryCode}");
			//trzeba podać albo NIP, albo PESEL:
			if (inp.Pesel != null) subject.Add($"2.5.4.5=PNOPL-{inp.Pesel}");
			else subject.Add($"2.5.4.5=TINPL-{inp.Nip}");

			_input.subjectName = string.Join(", ", subject);

			return Task.CompletedTask;
		}
	}
}
