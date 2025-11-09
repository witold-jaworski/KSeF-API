using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KSeF.Client.Clients;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.Core.Models.Certificates;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Klasa dostarcza klientowi API KSeF certyfikaty publiczne, których wymaga podczas inicjalizacji
	//instancja tej klasy powinna być zarejestrowana za pomocą DI dla ICryptographyClient, jako Singleton
	internal class PublicCertificatesProvider : CryptographyClient, ICryptographyClient
	{
		private const string LOCAL_CERTS = "PublicCertificates.json"; //plik z lokalnymi certyfikatami (używany, gdy jesteś OFFLINE)
				
		//Trochę paradoksalnie, to pole statyczne ma nadawaną wartość w konstruktorze - bo ten obiekt jest używany jako Singleton
		protected static ILogger? _logger;

		public PublicCertificatesProvider(IRestClient restClient, ILogger<PublicCertificatesProvider> logger) :base(restClient) 
		{
			_logger = logger;
		}

		//Procedura wymagana: dostarcza publiczne certyfikaty konstruktorowi klasy (singleton) CryptographyService - por. AddKSeFClient()
		public new async Task<ICollection<PemCertificateInfo>> GetPublicCertificatesAsync(CancellationToken cancellationToken)
		{
			ICollection<PemCertificateInfo> local = [];		//lista certyfikatów odczytana z dysku
			ICollection<PemCertificateInfo> current = [];   //ew. lista certyfikatów pobrana z serwera


			DateTimeOffset? validTo = null;
			string path = Program.FullPath(Path.Combine(Program.Config["CertsFolder"] ?? "", LOCAL_CERTS)); //ścieżka do pliku z certyfikatami
			if (File.Exists(path))
			{
				local = JsonUtil.Deserialize<ICollection<PemCertificateInfo>>(File.ReadAllText(path)); //odczytaj z pliku gotową listę
				validTo = local.Min(x => x.ValidTo);
			}
			
			if (!Program.IsRunningLocal || validTo == null) //validTo == null tylko wtedy, gdy nie ma w ogóle pliku z certyfikatami
			{
				var daysBefore = Program.Config.GetValue<int>("CertsCheckBefore");
				if (validTo == null || validTo < DateTimeOffset.Now.AddDays(daysBefore))
				{ //Spróbuj pobrać certyfikaty z serwera. Jak nie będzie łączności - trudno, zatrzyma całe KSeF.Services z błędem krytycznym
					current = await base.GetPublicCertificatesAsync(cancellationToken).ConfigureAwait(false);
					if (validTo == null || current.Min(x => x.ValidTo) > validTo) //Jeżeli pobrane certyfikaty są "świeższe" - zapisz je w miejsce dotychczasowych
					{
						File.WriteAllText(path, JsonUtil.Serialize<ICollection<PemCertificateInfo>>(current));
						local = current;
						validTo = current.Min(x => x.ValidTo);
						_logger?.LogInformation("Downloaded new public certificates, valid to {validTo}", validTo?.ToString("yyyy-MM-dd HH:mm:ss \"GMT\"zzz"));
					}
				}
			}
			_logger?.LogInformation("Activating KSeF.Client services from the CIRFMF library");
			return local;
		}
	}
}
