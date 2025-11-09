using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using KSeF.Client.Core.Interfaces.Rest;
using KSeF.Client.DI;
using KSeF.Client.Http;

namespace KSeF.Services.Test
{
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Rejestruje wszystkie potrzebne serwisy testowe
		/// </summary>
		public static IServiceCollection AddTestClient(this IServiceCollection services, string targetUrl)
		{

			var options = new KSeFClientOptions
			{
				BaseUrl = targetUrl 
			};

			services.AddSingleton(options);

			services
				.AddHttpClient<IRestClient, RestClient>(http =>
				{
					http.BaseAddress = new Uri(options.BaseUrl);
					http.DefaultRequestHeaders.Accept.Add(
						new MediaTypeWithQualityHeaderValue("application/json"));
				});

			//Przypisanie klas do żądań, deklarowanych w ich atrybutach 
			//(Wyłącznie z tej samej przestrzeni nazw, co klasa wywołująca tę metodę klasy HandlerProvider)
			HandlerProvider.InitializeFor(typeof(ServiceCollectionExtensions)); //W typeof() wskazuj klasę wywołującą

			//Zarejestrowanie serwisu, który tworzy obiekty do obsługi żądań Klienta
			services.AddScoped<IRequestHandlerProvider, HandlerProvider>();

			return services;
		}
	}
}
