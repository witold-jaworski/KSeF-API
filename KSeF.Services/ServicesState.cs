using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services
{
	//Pomocnicza klasa, wykorzystywana do diagnostyki
	//Jej pojedyncza instancja przypiasana do Program, jej stan jest wysyłany do potoku *.sta, obsługiwanego przez serwis Indicator.
	//Zmieniana przez serwis Worker i wywoływane przez niego metody/obiekty potomne.
	public class ServicesState
	{
		//Możliwe stany serwisów tego programu (w komentarzu dalsza część tekstu statusu. Trzy znaki po
		//następujące zaraz po symbolu to kropki lub spacje. Ostatnia spacja oznacza oczekiwanie na reakcję
		//Klienta - powinien czegoś zażądać, coś wpisać, lub coś odczytać)
		public const string INIT = "INIT"; //'...'		inicjalizacja serwisów
		public const string STBY = "STBY"; //'   '		gotowy ("standby") do przetwarzania żądań 
		public const string NREQ = "NREQ"; //'.  (req)' Otrzymano nowe żądanie (req)
		public const string WRTE = "WRTE"; //'.. (req)' Wpisz dane wejściowe dla żądania req
		public const string PRCS = "PRCS"; //'...(req)' przetwarza żądanie ("processing")
		public const string WAIT = "WAIT"; //'...(req)' oczekuje na informacje zwrotną z (np. z KSeF API)
		public const string READ = "READ"; //'   (req)' rezultat gotowy do odczytania
										   // w normalnym cyklu po odczytaniu rezultatu następuje powrót do STBY
		public const string EXIT = "EXIT"; //'...'		jesteśmy w trakcie kończenia działania hosta (tj. programu)

		//dane instancji
		private string _state = string.Empty; //w czasie dzianiania - jedna ze stałych, wyliczonych powyżej
		private DateTime _updated = DateTime.MinValue; //Data i czas ostatniej zmiany stanu
		private string _request = "";   //aktualnie przetwarzane żądanie (występuje tylko dla state = PRCS, WAIT, READY)
		//private bool _busy = false; //pomocnicza flaga, aby na pewno Indicator nie odczytał niekompletnych danych (bo były akurat zmieniane przez Worker)
		private static readonly object _busy = new();  //semafor
		
		//Konstruktor
		internal ServicesState ()
		{
			Set(INIT, null);
		}
		//Odnotowuje zmianę stanu serwisów
		//Argumenty:
		//	state:		nowy stan (jedna ze stałych tej klasy)
		//  logger:		opcjonalnie: jeżeli podany, zapisuje do logu zmianę stanu (na poziomie Debug)
		//	request:	opcjonalny: aktualne żądanie (jeżeli dotyczy)
		//	handler:	opcjonalny: obiekt handlera (jeżeli przydzielony)
		internal void Set(string state, ILogger? logger, string request = "", IRequestHandler? handler = null)
		{
			lock (_busy) //Na wszelki wypadek
			{
				_state = state;
				_request = request;
				_updated = DateTime.Now;
			}

			logger?.LogTrace("Program state set to: {state}", AsString(handler));
		}

		//Pomocnicza funkcja aby w razie czego sprawdzać stan tego hosta
		//Argumenty:
		//	state:	jedna ze stałych tej klasy, do porównania
		//zwraca true, gdy aktualny stan jest równy <state>
		internal bool Is (string state) 	{ return _state == state; }

		//Zwraca stan w postaci stringu (taki Indicator wpisuje w potok *.sta)
		//	handler:	opcjonalny: obiekt handlera (jeżeli przydzielony) - tylko do odnotowania jego typu
		internal string AsString(IRequestHandler? handler = null)
		{
			string tail = _request == "" ? "" : $", '{_request}'";
			if (handler != null) tail += $" is being handled by {handler}";

			//Po 4-znakowym kodzie stanu następuje trójznakowa, pomocznicza "klasa oczekiwania":
			//którą Klient może wykorzystać w logice komunikacji z serwerem.
			//3 spacja oznacza "oczekuję na akcję Klienta"
			string wait = 
			_state switch
			{
				STBY or READ => "   ",
				NREQ => ".  ",
				WRTE => ".. ",
				_ => "...",
			};
			return $"{_state}{wait} (set: {_updated.ToString("HH:mm:ss.fff")}{tail})";
		}
	}
}
