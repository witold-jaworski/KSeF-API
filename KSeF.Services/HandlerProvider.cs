using KSeF.Services.Test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSeF.Client.Core.Interfaces.Rest;

namespace KSeF.Services
{
	//Atrybut, który należy użyć w "dekoratorze" klasy do przypisania jej obsługi odpowiedniego żądania
	//W konstruktorze podajesz tekst żądania, a atrybut zwraca go we właściwości Request
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
	public class HandlesRequestAttribute(string request) : Attribute
	{
		public const string ALL = "*";	//Specjalna stała, oznaczająca wszystkie (pozostałe) żądania
										//(Przeznaczona dla ewentualnego "handlera domyślnego")	
		/* Użycie w dekoratorze klasy:
						 [HandlesRequest("\posts")]
		 */
		private readonly string _request = request;
		public string Request { get {return _request;} }
	}

	//Implementacja tworzenia obiektów do obsługi żądań
	public class HandlerProvider(IRestClient restClient): IRequestHandlerProvider
	{
		protected readonly IRestClient _restClient = restClient;
		static protected readonly Dictionary<string, Type> _handlers = [];  //Słownik klas zarejestrowanych
																			//za pomocą dekoratora z atrybutem
																			//HandlesRequest("żądanie")
		//Wypełnianie słownika żądań na podstawie atrybutów 'HandlesRequest',
		//umieszczonych w dekoratorach przy odpowiednich klasach. Wywołaj przed zarejestrowaniem tego serwisu w hoście.
		//Argumenty:
		//	info:	jakakolwiek klasa należąca do przestrzeni nazw, z której mają być klasy rejestrowane w _handlers. 
		//			 Możesz tu podstawić klasę rozszerzeń, która wywołuje tę metodę.
		//Uwaga: rejestrowane są handlery, których przestrzenie nazw ZACZYNAJĄ się od przestrzeni nazw typu <info>.
		//np. gdy Namespace <info> to 'Ksef.Services.Api', to metoda zarejestruje klasy z dwóch przestrzeni nazw:
		//Ksef.Services.Api
		//Ksef.Services.Api.Local
		public static void InitializeFor(Type info)
		{
			if (_handlers.Keys.Count > 0) return;  //Tę metodę można wywołać tylko raz
			Debug.Assert(info.Name != null,$"Wrong reference object {info} with no namespace passed as the reference for request handlers registering.");
			if (info.Namespace == null) return;	//coś poszło bardzo źle - lepiej niczego nie rejestrować.

			foreach(Type tp in info.Assembly.GetTypes())
				if(tp.Namespace != null && tp.Namespace.StartsWith(info.Namespace))
					foreach(Attribute at in tp.GetCustomAttributes(typeof(HandlesRequestAttribute), false)) //Jedna klasa może obsługiwać
																											//kilka żądań (ma kilka atrybutów):
					{
						var hra = (HandlesRequestAttribute) at;
						var key = hra.Request.ToLower(); //Symbole żądań nie odróżniają dużych i małych liter
						if(_handlers.ContainsKey(key)) //Czy ktoś drugi raz implementuje obsługę tego samego żądania!?
						{
							Debug.Assert(false, $"Class '{tp.FullName}' declares handling request '{hra.Request}', " + 
												$"which is alerady assigned to class '{_handlers[key].FullName}'.");
							_handlers[key] = tp; //w produktywnej wersji - podmieniamy te klasy bez gadania.
						}
						else _handlers.Add(key, tp);
					}

		}
#pragma warning disable CS8600, CS8602 // Possilbe use of null 
		public IRequestHandler GetHandlerFor(string request, IServiceProvider services)
		{
			IRequestHandler handler;

			var key = request.ToLower();

			if (_handlers.ContainsKey(key))
				handler = (IRequestHandler) Activator.CreateInstance(_handlers[key]);
			else if (_handlers.ContainsKey(HandlesRequestAttribute.ALL))
				handler = (IRequestHandler) Activator.CreateInstance(_handlers[HandlesRequestAttribute.ALL]);
			else
				throw new NotImplementedException($"No IRequestHandler class has been registered for '{request}' request.");

			handler.Assign(request, services);

			return handler;	
		}
	}
}
