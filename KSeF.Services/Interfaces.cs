using KSeF.Client.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Interfejsy, wykorzystywane w serwisie Worker

namespace KSeF.Services
{   //interfejsy są na tyle ogólne, że, podobnie jak całe to środowisko, mogą służyć do obsługi czegoś innego:

	//Interfejs implementowany przez obiekty obsługujące jakieś żądanie otrzymane przez Worker
	public interface IRequestHandler
	{
		//Przypisanie obiektowi zadania do wykonania (inicjalizacja)
		//Argumenty:
		//	request:	żądanie do obsłużenia
		//	services:	tak naprawdę to <scope> z zakresu otworzonego w serwisie Worker do obsługi tego żądania
		//				Handler może od niej zarządać innych serwisów dostępnych w tej sesji (IKSeFClient, IRestClient, ...).
		public void Assign(string request, IServiceProvider services);
		//Zwraca true, gdy należy wywołać metodę PrepareInput
		public bool RequiresInput { get; }
		//Przygotowuje otrzymane dane do dalszego przetwarzania (w metodzie Process)
		//Argumenty:
		//	data:		tekst, zawierający dane (np. w JSON)
		//	stopToken:	ewentualny token do przerwania przetwarzania asynchronicznego (zalecany przez .NET)
		public Task PrepareInputAsync(string data, CancellationToken stopToken);

		//Przetważa żądanie (np. wysyła je do serwera API KSeF i czeka na odpowiedź)
		//Argumenty:
		//	stopToken:	ewentualny token do przerwania przetwarzania asynchronicznego (zalecany przez .NET)
		public Task ProcessAsync(CancellationToken stopToken);

		//Zwraca true, jeżeli można wywołać SerializeResults()
		public bool HasResults { get; }
		
		//Zwraca rezultat w postaci tekstu (np. JSON)
		public string SerializeResults();
	}

	//Interfejs zwracający obiekt, którym można przetworzyć żądanie otrzymane przez Worker
	public interface IRequestHandlerProvider
	{
		//Zwraca obiekt, który ma obsłużyć podane żądanie
		//Argumenty:
		//	request:	żądanie (np. ścieżka REST)
		//	services:	obiekt, od którego można żądać innych serwisów obecnych na hoście.
		IRequestHandler GetHandlerFor(string request, IServiceProvider services);
	}
}
