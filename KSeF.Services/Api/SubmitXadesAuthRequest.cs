using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Wysyła do KSeF wypełniony formularz XML z wnioskiem o uwierzytelnienie. Wołać po GetAuthChallenge."
	[HandlesRequest("SubmitXadesAuthRequest")]
	internal class SubmitXadesAuthRequest : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public bool VerifyCertificationChain { get; set; } = false;

			//Wariant 1: podpisany XML
			public string? SignedRequestFile { get; set; } //ścieżka do pliku z podpisanym plikiem XML żądania 
			public string? SignedRequestBase64 { get; set; } //Podpisane żądanie (XML jako dane binarne, enkodowane w Base64)

			//Wariant 2: sam stwórz i podpisz  (zapewne certyfikatem KSeF) XML z żądaniem
			public string? Challenge { get; set; } //wynik popoprzedniego kroku
			public string? Nip { get; set; } //identyfikator kontekstu
			public string? Iid { get; set; } //identyfikator kontekstu
			public string? NipVatUe { get; set; } //identyfikator kontekstu
			public string? CertificatePem { get; set; } //certyfikat uwierzytelniający
			public string? PrivateKeyPem { get; set; } //klucz prywatny (gdy nie ma go w certyfikacie)
			public bool UseFingertip { get; set; } = false; 
		}
		/* UWAGI: 
		 * W Wariancie 1 należy pdodać JEDNO z pól: signedRequestFile LUB base64SignedRequest 
		 * pola: challenge, certificatePem, oraz (nip | iid | nipVatUE) są obowiązkowe w Wariancie 2
		 */

		//	Rezultat:	verbatim z KSeF.Client (SignatureResponse)

		//Struktura wewnętrzna, na przetworzone dane wejściowe: 
		protected class Params
		{
			public string signedXml = "";
			public bool verifyCertificateChain = false;
		}
		//----------------------
		protected Params _params = new();
		protected SignatureResponse _output = new();
		public override bool RequiresInput { get { return true; } }

		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var input = JsonUtil.Deserialize<InputData>(data);
			if (input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			if(input.Challenge != null) //Zbuduj w pamięci plik XML i go podpisz
			{
				var unsignedXml = BuildAuthRequest(input.Challenge, input.Nip, input.Iid, input.NipVatUe, input.UseFingertip);
				var certificate = LoadCertificate(null, certPem: input.CertificatePem, null, pkeyPem: input.PrivateKeyPem);

				var signatureService = Scope.GetRequiredService<ISignatureService>();
				_params.signedXml = signatureService.Sign(unsignedXml, certificate);
			}
			else //otrzymaliśmy ścieżkę do podpisanego pliku XML
			{
				if (input.SignedRequestFile != null)
					_params.signedXml = File.ReadAllText(ValidateForInput(input.SignedRequestFile, "signedRequestFile"));
				else
					if (input.SignedRequestBase64 != null)
					_params.signedXml = Encoding.UTF8.GetString(Convert.FromBase64String(input.SignedRequestBase64));
				else
					throw new InvalidDataException("Missing input XML: you must provide the 'signedRequestFile' path or the 'signedRequestBase64'");
			}

			_params.verifyCertificateChain = input.VerifyCertificationChain;

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			_output = await _ksefClient.SubmitXadesAuthRequestAsync(_params.signedXml, 
																	_params.verifyCertificateChain, 
																								  stopToken);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}
	}
}
