using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Globalization;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Http;

namespace KSeF.Services.Api.Local
{
	[HandlesRequest("#GetInvoiceQrLink")]
	internal class GetInvoiceQrLink: GetCertificateQrLink  //Generowanie QR I różni się tak niewiele od QR II, że implementuję je jako klasę potomną.
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		new protected class InputData
		{
			public string? InvoiceFile { get; set; } //wyznacz link dla tego pliku z fakturą
			public string? InvoiceBase64 { get; set; } //XML faktury, tylko binarnie - np. odczytany jako dane binarne z pliku i enkodowany w Base64
			public string? SaveToPng { get; set; }  //ścieżka do wynikowego pliku *.png, w którym program ma zapisać kod QR 
			public int PixelsPerDot { get; set; } = 20; //rozmiar pojedynczego kwadratu QR (w pikselach) - potem jest zmniejszany, więc musi być duży aby było wyraźnie.
			public int ImageSize { get; set; } = 300; //rozmiar kodu QR (w pikselach) - ta wartość zmniejsza rozmycie
		}
		/*
		 UWAGI: 
			1. "invoiceFile" i "invoiceBase64" to alternatywy
			2.	Jeżeli nie podano "saveToPng", program zwróci tylko w url tekst linku, który można zakodowac w kodzie QR.
		*/

		//W tej klasie generujemy inny Url niż w przypadku certyfikatu:
		protected override string GenerateUrl(IVerificationLinkService linkService)
		{
			return linkService.BuildInvoiceVerificationUrl(_input.nip, _input.issueDate, _input.invoiceHash);
		}

		//--- z całego interfejsu zmianie ulega tylko PrepareInput: -----
		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			//Zamień przekazaną fakturę na bajty:
			byte[] bytes = GetBytes(input.InvoiceBase64, input.InvoiceFile, "invoiceFile");

			//Oblicz jej hash:
			_input.invoiceHash = Scope.GetRequiredService<ICryptographyService>().GetMetaData(bytes).HashSHA;

			//Wyciagnij NIP i P_1 z faktury:
			var xml = XmlFromBytes(bytes);
		
			_input.nip = xml.SelectSingleNode(NIP_XPATH)?.InnerText;
			string? invDate = xml.SelectSingleNode(P_1_XPATH)?.InnerText;
			if (invDate != null)
				DateTime.TryParseExact(invDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _input.issueDate);

			PrepareImageProcessing(input.SaveToPng, input.PixelsPerDot, input.ImageSize);

			return Task.CompletedTask;
		}
	}
}
