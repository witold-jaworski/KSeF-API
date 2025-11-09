using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using KSeF.Services.Api.Local;
using KSeF.Client.Extensions;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.DI;

namespace KSeF.Services.Api
{
	public static class ServiceCollectionExtensions
	{
		// Rejestruje wszystkie serwisy KSeF (lokalne i zdalne)
		//Argumenty:
		//	targetUrl:	url do odp. serwera KSeF (TEST, DEMO lub PROD)
		//	mode:		aktualny tryb działania programu (taki, jaki został przekazany w argumentach - np. "Local:TEST")
		public static IServiceCollection AddApiClient(this IServiceCollection services, string targetUrl, string mode)
		{
			services.AddSingleton<ICryptographyClient, PublicCertificatesProvider>(); //Koniecznie dodaj przed inicjalizacją klientów 
																					  
			//Wywołaj standardowe metody Klienta KSeF:
			services.AddKSeFClient(options => {	options.BaseUrl = targetUrl; });

			//Wewnętrznie ta metoda KSeF umieszcza w CryptographyService odwołanie do ICryptographyClient z naszego PublicCertificateProvider:
			services.AddCryptographyClient(); //zostawiam domyślną WarmupMode = Blocking - aby mieć pewność, że się zainicjowało

			//Linia wymagana, aby ISignatureService "umiało" podpisywać certyfikatami z kluczem ECDsa.
			CryptoConfig.AddAlgorithm( typeof(Ecdsa256SignatureDescription), "http://www.w3.org/2001/04/xmldsig-more#ecdsa-sha256");

			//Przypisanie klas do żądań deklarowanych w ich atrybutach 
			if (mode.StartsWith("local:", StringComparison.InvariantCultureIgnoreCase))
			{   //zarejestruj żądania z tylko  przestrzeni nazw Ksef.Servies.Api.Local:
				HandlerProvider.InitializeFor(typeof(GetMetadata)); //W typeof() wpisz jakąkolwiek klasę z odpowiedniej przestrzeni nazw
			}
			else //jesteśmy Online:
			{	//zarejestruj żądania z przestrzeni nazw: Ksef.Servies.Api, Ksef.Servies.Api.Local (bo zagnieżdżona)
				HandlerProvider.InitializeFor(typeof(ServiceCollectionExtensions)); 
			}
			//Zarejestrowanie serwisu, który tworzy obiekty do obsługi żądań Klienta
			services.AddScoped<IRequestHandlerProvider, HandlerProvider>();

			return services;
		}
	}

}
