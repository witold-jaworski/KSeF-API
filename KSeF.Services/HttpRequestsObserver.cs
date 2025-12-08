using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace KSeF.Services
{
	//Klasy do szczegółowego śledzenia żądań HTTP.
	//Oparte na rozwiązaniu przedstawionym w https://www.meziantou.net/observing-all-http-requests-in-a-dotnet-application.htm
	public class HttpRequestsObserver(ILogger logger) : IDisposable, IObserver<DiagnosticListener>
	{
		private IDisposable? _subscription = null;
		private readonly ILogger _logger = logger;

		public void OnNext(DiagnosticListener value)
		{
			if (value.Name == "HttpHandlerDiagnosticListener") 
				if(_logger.IsEnabled(LogLevel.Debug)) //Nasz Listener tworzy wpisy na tylko na poziomie Debug i Trace
				{                                     //więc nie ma sensu włączać obserwacji, gdy poziom jest wyższy.
													  //(Gdy poziom jest Trace, IsEnabled(LogLevel.Debug) zwraca true)
					Debug.Assert(_subscription == null);
					_subscription = value.Subscribe(new HttpHandlerDiagnosticListener(_logger));
				}
		}

		public void OnCompleted() { }
		public void OnError(Exception error) { }

		public void Dispose()
		{
			_subscription?.Dispose();
		}

		private sealed class HttpHandlerDiagnosticListener(ILogger logger) : IObserver<KeyValuePair<string, object?>>
		{
			private const int BUFFERING_TIMEOUT = 500; //max. timeout na buforowanie zawartości (HttpContent): 500 milisekund
			private const int MAX_CONTENT_LENGTH = 8192; //zawartość dłuższa od tej granicy nie jest wpisywana do logu

			private static readonly Func<object, HttpRequestMessage> RequestAccessor = CreateGetRequest();
			private static readonly Func<object, HttpResponseMessage> ResponseAccessor = CreateGetResponse();
			private readonly ILogger _logger = logger;

			public void OnCompleted() { }
			public void OnError(Exception error) { }

			//Pokazywać w logu nagłówki żądań HTTP?
			public bool ShowHeaders { get { return _logger.IsEnabled(LogLevel.Trace); } }

			//Pokazywać w logu dane żądań HTTP?
			public bool ShowContent { get { return _logger.IsEnabled(LogLevel.Trace); } }

			//Odnotowuje w logu żądanie i odpowiedź HTTP. Dla poziomu logowania Trace zapisuje je z nagłówkami HTTP:
			public void OnNext(KeyValuePair<string, object?> value)
			{
				// Obydwa zdarzenia są wywoływane ze środka metody HttpClient.SendAsync():
				// UWAGA: podczas debugowania staraj się nie przekroczyć timeoutu, jaki HttpClient ma na obsługę żądania (100s),
				// bo wtedy uzna, że zostało nieobsłużone i zgłosi odpowiedni wyjatek.
				if (value.Key == "System.Net.Http.HttpRequestOut.Start")
				{
					// The type is private, so we need to use reflection to access it.
					if (value.Value == null) return;
					var request = RequestAccessor(value.Value);
					if (request != null)
						_logger.LogDebug("Sending HTTP request:\n {request}", FormatForLog(request, ShowHeaders, ShowContent));
				}
				else if (value.Key == "System.Net.Http.HttpRequestOut.Stop")
				{
					// The type is private, so we need to use reflection to access it.
					if (value.Value == null) return;
					var response = ResponseAccessor(value.Value);
					if (response != null)
						_logger.LogDebug("Received HTTP response:{response}", FormatForLog(response, ShowHeaders, ShowContent));
				}
			}

			//W logu umieszczamy tylko dane typów z tego wyliczenia (jest szansa, że będą małe):
			private const string LOGGED_MEDIA_TYPES = "text/plain;application/xml;application/json";

			//Pomocnicza funkcja, pozwalająca "podejrzeć" zawartość (body) wiadomości HTTP.
			//Argumenty:
			//	content: zawartość żądania / odpowiedzi.
			//W środku "synchronizuję" metodę LoadIntoBufferAsync() z określonym timeoutem. 
			//Tak, to "brzydka" technika, ale ta funkcja będzie tylko wywoływana gdy zostanie włączone logowanie 
			//na poziomie Debug lub Trace - a więc nigdy w środowisku produkcyjnym.
			private static string ToString(HttpContent content)
			{
				/* Pomysł na sprawdzanie długości danych był dobry, ale ani KSeF.Client, ani serwer jej nie podają:
				 * long? length = content.Headers.ContentLength;
				if (length == null || length == 0) return string.Empty;
				if (length > MAX_CONTENT_LENGTH) return $"\t\t-- content longer than {MAX_CONTENT_LENGTH} bytes is not logged --"; */

				//jedyne, co możemy wcześniej sprawdzić, to typ zawartości:
				string? contentType = content?.Headers?.ContentType?.MediaType;
				if (content == null || contentType == null || ! LOGGED_MEDIA_TYPES.Contains(contentType)) 
																				return "\t\t--- cannot log this kind of data  --- ";

				//musimy zbuforować ten HttpContent, aby podczas przetwarzania mógł być odczytany więcej niż raz.
				if (content.LoadIntoBufferAsync().Wait(BUFFERING_TIMEOUT)) //Nie ma synchronicznej wersji tej metody, więc muszę użyć Wait()															   
				{
					using var stream = new MemoryStream();//OK, skopiuj teraz zawartość do MemoryStream...	
					content.CopyTo(stream, null, CancellationToken.None);
					var contentReader = new StreamReader(stream);
					contentReader.BaseStream.Seek(0, SeekOrigin.Begin);//Po skopiowaniu stream "przewinięty" do końca

					//...a z MemoryStream - odczytaj do tekstu:
					string contentText = contentReader.ReadToEnd();

					//Na koniec: maskowanie w tekście ewentualnych tokenów 
#if !DEBUG			//(wyłączam dla wersji deweloperskiej) 
					if(contentText == "application/json") LoggerExtensions.MaskRestrictedFields(ref contentText);
#endif
					return $"\t\t{contentText.Replace("\n", "\n\t\t")}";
				}
				else return "\t\t--- cannot buffer this data (timeout) --- ";
			}

			//Pomocnicza: czytelnie sformatowany opis żądania HTTP
			//Argumenty:
			//	request:		żądanie HTTP do opisania
			//	includeHeaders:	opcjonalny: flaga, czy pokazać w opisie nagłówki HTTP
			//  includeContent:	opcjonalny: flaga, czy pokazać w opisie dane żądania
			private static string FormatForLog(HttpRequestMessage request, bool includeHeaders = false, bool includeContent = false)
			{
				var str = new StringBuilder();
				str.AppendLine($"\tURL: {request.RequestUri}");
				str.AppendLine($"\tMethod: {request.Method}");
				if (includeHeaders)
				{
					if (request.Headers != null)
						str.AppendLine($"\tHTTP Headers:\n\t{request.Headers.ToString().Replace("\n", "\n\t")}");
					else
						str.AppendLine("\t-- no HTTP headers --");
				}
				if (request.Content != null)
				{
					str.AppendLine($"\tData:\t{request.Content.Headers.ToString().Replace("\n", "\n\t\t")}");
					if (includeContent) str.AppendLine(ToString(request.Content));
				}
				else
					str.AppendLine("\t-- no data --");

				return str.ToString();
			}

			//Pomocnicza: czytelnie sformatowany opis odpowiedzi HTTP
			//Argumenty:
			//	response:		odpowiedź HTTP do opisania
			//	includeHeaders:	opcjonalny: flaga, czy pokazać w opisie nagłówki odpowiedzi HTTP
			//  includeContent:	opcjonalny: flaga, czy pokazać w opisie dane odpowiedzi
			private static string FormatForLog(HttpResponseMessage response, bool includeHeaders = false, bool includeContent = false)
			{
				var str = new StringBuilder();
				str.AppendLine($"\t{(int) response.StatusCode} ({response.StatusCode})"); //Pierwsza linia jest kontynuacją stałego tekstu
				if (response.Content != null)
				{
					str.AppendLine($"\tData:\t{response.Content.Headers.ToString().Replace("\n", "\n\t\t")}");
					if (includeContent) str.AppendLine(ToString(response.Content));
				}
				else
					str.AppendLine("\t-- no data --");

				if (includeHeaders)
				{
					if (response.Headers != null)
						str.AppendLine($"\n\tHTTP response headers:\n\t{response.Headers.ToString().Replace("\n", "\n\t")}");
					else
						str.AppendLine("\t-- no HTTP response headers --");
				}

				return str.ToString();
			}
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return
#pragma warning disable IDE0350 // Use implicitly typed lambda
			private static Func<object, HttpRequestMessage> CreateGetRequest()
			{
				var requestDataType = Type.GetType("System.Net.Http.DiagnosticsHandler+ActivityStartData, System.Net.Http", throwOnError: true);
				var requestProperty = requestDataType?.GetProperty("Request");
				return (object o) => (HttpRequestMessage)requestProperty?.GetValue(o);
			}

			private static Func<object, HttpResponseMessage> CreateGetResponse()
			{
				var requestDataType = Type.GetType("System.Net.Http.DiagnosticsHandler+ActivityStopData, System.Net.Http", throwOnError: true);
				var requestProperty = requestDataType?.GetProperty("Response");
				return (object o) => (HttpResponseMessage) requestProperty?.GetValue(o);
			}
		}
	}
}
