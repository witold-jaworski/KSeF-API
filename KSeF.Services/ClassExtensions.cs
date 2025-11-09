using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

//Moduł na różne statyczne klasy z metodami rozszerzającymi funkcjonalność klas standardowych 
namespace KSeF.Services
{
	public enum PemSection
	{
		CERTIFICATE,
		PRIVATE_KEY,
		PUBLIC_KEY
	}

	//Drobne rozszerzenie, zamieniające podkreślenia w wyliczeniu PemSection na spacje:
	public static class PemSectionNames
	{
		public static string AsString(this PemSection section)
		{ return section.ToString().Replace('_', ' '); }
	}

	//Metody podmieniające znaki końca linii w tekstach.
	//Stworzone z myśla o komunikacji poprzez potoki nazwane - tam jedyną jednostką odczytu jest "linia"
	//więc trzeba w tekście wejścowym podmienić znaki \cr i \n na jakieś inne, a przy wczytywaniu - zamienić je z powrotem w \r  i \n
	public static class StringExtensions
	{
		private static int xCR = -1; //-1 oznacza: nie zainicjalizowany
		private static int xLF = -1; //(co prawda w konfiguracji programu powinno wszystko być na miejscu, ale gdy ktoś to błędnie wywoła...)

		//Inicjalizuje klasę przy pierwszym użyciu odpowiedniej metody:
		private static bool IsInitialized()
		{
			if (xCR == 0 || xLF == 0) return false;	//Inicjalizacja się nie udała
			if (xLF < 0 || xCR < 0) //Wołany po raz pierwszy: spróbuj zainicjalizować:
			{
				xCR = Program.Config.GetValue<int>("EOLReplacements:CR"); //Jeżeli nie ustawiono jeszcze konfiguracji - ta linia spowoduje wyjątek
				xLF = Program.Config.GetValue<int>("EOLReplacements:LF");
			}
			return true;
		}

		//Zastępuje w otrzymanym tekście znaki \r i \n "umówionymi z Klientem" zamiennikami, i zapisuje jako (technicznie) pojedynczą linię
		public static string? AsSingleLine(this string? multiline)
		{
			if (!IsInitialized()) return null;
			if (multiline == null) return null; 
			return multiline.Replace('\n', (char)xLF).Replace('\r', (char)xCR);
		}

		//Zastępuje w tekście "umówione z Klientem" zamienniki znakami \r i \n , 
		public static string? AsMultiLine(this string? encoded)
		{
			if (!IsInitialized()) return null;
			if(encoded == null) return null;	
			return encoded.Replace((char)xLF, '\n').Replace((char)xCR, '\r');
		}


		//Specyficznych wyszukiwań nigdy za wiele:
		//Znajdowanie początku sekcji w pliku PEM
		//Argumenty:
		//	pem:		preszukiwany tekst w formacie PEM
		//	section:	sekcja PEM ("PRIVATE KEY", "PUBLIC KEY", "CERTIFICATE")
		private static int BeginPemSection(this string pem, string section)
		{
			var pos = pem.IndexOf(section);
			if (pos < 0) return -1;
			pos = pem.LastIndexOf('\n', pos); //Pierwszy znak nowej linii poprzedzający <section>
			if (pos < 0) return 0;
			else return pos + 1;
		}
		//Znajdowanie końca sekcji w pliku PEM
		//Argumenty:
		//	pem:		preszukiwany tekst w formacie PEM
		//	section:	sekcja PEM ("PRIVATE KEY", "PUBLIC KEY", "CERTIFICATE")
		private static int EndPemSection(this string pem, string section)
		{
			var pos = pem.LastIndexOf(section, pem.Length-1);
			if (pos < 0) return -1;
			pos = pem.IndexOf('\n', pos); //Pierwszy znak nowej linii poprzedzający <section>
			if (pos < 0) return pem.Length -1;
			else return pos;
		}
		//Zwraca zawartość wybranej sekcji z pełnego tekstu zawartości pliku *.pem (może się składać z kilku części)
		//Argumenty:
		//	section:	wybór poszukowanej sekcji. Gdy wybierzesz PRIVATE_KEY,
		//				zwróci każdy napotkany rodzaj: --RSA PRIVATE KEY--, --EC PRIVATE KEY--, itd
		//  asBase64:	opcjonalny. Zwróci samą zawartość, bez dekoratorów i znaków nowej linii
		public static string? GetPemSection(this string? pem, PemSection section, bool asBase64 = false)
		{
			if (pem == null) return null;
			var label = section.AsString();
			var begin = BeginPemSection(pem, label);
			if(begin < 0) return null; //nie ma takiej sekcji
			var end = EndPemSection(pem, label);
			Debug.Assert(end > begin);
			if (asBase64)
			{
				begin = pem.IndexOf('\n', begin);
				end = pem.LastIndexOf('\n', end - 1);
				Debug.Assert(begin > 0); Debug.Assert(end > begin);
				pem = pem.Substring(begin+1, end - begin-1);
				return pem.Replace("\r", "").Replace("\n", "");
			}
			else
			{
				return pem.Substring(begin, end - begin + 1).Replace("\r", ""); //Na wszelki wypadek: usuń wszystkie CR, znaki nowej linii to tylko LF
			}
		}
	}

	public static class DateTimeExtensions
	{
		//Zwraca timestamp (UTC), w znormalizowanym formacie tekstowym
		public static string GetTimestamp(this DateTime dateTime)
		{
			return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
		}
	}

	//Serializacja wyjątków do Json
	public static class JsonExtensions
	{
		//Pomocnicza klasa - skrót do Dictionary<string, object?>, dodana wyłącznie dla większej czytelności poniższego kodu
		private class JsonBag : Dictionary<string, object?> { }		
		private static JsonBag ExceptionForJsonBuilder(Exception ex, int level = 0)
		{
			var exception = new JsonBag()
			{
				{"internalType", ex.GetType().ToString()},
				{"hResult", ex.HResult.ToString("x8")},
				{"exceptionDescription", ex.Message },
				{"exceptionSource", ex.Source??"" },
				{"exceptionTarget", ex.TargetSite?.ToString()??""}
			};
			if (ex.InnerException != null) 
					exception.Add("triggeredBy", ExceptionForJsonBuilder(ex.InnerException, level + 1));
			return exception;
		}

		//Metoda tworzy "ładniejszą" serializację od standardowej:
		//Argumenty:
		//	programState: status programu w chwili wstąpienia błędu
		public static string ToJson(this Exception ex, string? programState = "")
		{
			var exception = ExceptionForJsonBuilder(ex);

			var envelope = new JsonBag()
			{
				{"exception", exception},
				{"applicationState", programState??""},
				{"timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
			};

			string result = JsonSerializer.Serialize(envelope);
			return result.Replace(@"\u0027","'"); //TODO: na razie brutalnie przywracam "escapowane" apostrofy
		}

		//------ poniższe dwie funkcje są wykorzystywane w namespace Ksef.Services.Test (przez PostHandler) -------
		//Pomocnicze: parser danych wejściowych
		//Argumenty: 
		//	json: dane wejściowe
		public static object Parse(string json)
		{
			if (json.StartsWith('[')) return ParseList(json);
			else return ParseDictionary(json); 
		}

		//Pomocnicze: parser danych wejściowych
		//Argumenty: 
		//	json: dane wejściowe (zaczynające się od "{")
		private static ExpandoObject ParseDictionary(string json)
		{
			dynamic? input = JsonSerializer.Deserialize<ExpandoObject>(json);
			if (input == null) throw new ArgumentNullException($"Failed to parse received data:\n\t'{json}'");
			//Spróbuj rozwinąć ewentualne struktury wewnętrzne (umieszczam je jako stringi, z apostrofami zamiast cudzysłowów):
			IDictionary<string, object?> inp = input;
			string[] keys = inp.Keys.ToArray();
			foreach (var key in keys)
			{  //Json w .NET konwertuje zagnieżdżone słowniki w danych wejściowych jako pojedynczy JsonElement
			   //więc przesyłam je jako tekst w którym cudzysłowy są zastąpione spacjami.
			   //(to niestety trzeba powtarzać dla każdego zagnieżdżenia przesyłanej struktury) 
				if (inp[key] == null) continue;
				var elm = inp[key]; //Każdy element w tym słowniku to JsonElement
				if (elm == null) continue;
				var value = elm.ToString() ?? ""; //pomocnicza zmienna, by nie wydłużać warunku w if():
				if (value.StartsWith('[') || value.StartsWith('{'))
					inp[key] = Parse(value.Replace('\'', '"'));
			}
			return input;
		}
		//Pomocnicze: parser danych wejściowych
		//Argumenty: 
		//	json: dane wejściowe (zaczynające się od "[")
		private static List<object> ParseList(string json)
		{
			var input = JsonSerializer.Deserialize<List<ExpandoObject>>(json);
			List<object> result = [];
			if (input == null) throw new ArgumentNullException($"Failed to parse received data:\n\t'{json}'");
			//Spróbuj rozwinąć ewentualne struktury wewnętrzne (umieszczam je jako stringi, z apostrofami zamiast cudzysłowów):
			for (int i = 0; i < input.Count; i++)
			{   
				var elm = input[i]; //Każdy element w tym słowniku to JsonElement
				if (elm == null) continue;
				var value = elm.ToString() ?? ""; //pomocnicza zmienna, by nie wydłużać warunku w if():
				if (value.StartsWith('[') || value.StartsWith('{'))
					result.Add(Parse(value.Replace('\'', '"')));
				else result.Add(elm);
			}
			return result;
		}
		//Pomocnicze: odczytanie wartości tekstowej ze słownika
		//Argumenty:
		//	key:		klucz słownika
		//	fallback:	[opcjonalny] wartość zwrócacana przez funkcję, gdy to pole słownika jest null
		//UWAGI: funkcja dodana, aby VS Code Analyzer "odczepił" się ze swoimi poradami (messages)
		public static string AsString(this IDictionary<string, object?> dict, string key, string fallback = "")
		{
			var tp = dict[key]; //zgłosi wyjątek, jeżeli klucz nie istnieje
			if(tp == null) return fallback;
			return tp.ToString()??fallback;
		}

		//Opcje serializacji wyników metod
		private static readonly JsonSerializerOptions _jsonOutputSettings = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowOutOfOrderMetadataProperties = true,
			WriteIndented = false,
			PropertyNameCaseInsensitive = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Converters = { new JsonStringEnumConverter() }
		};

		//Serializuje klasę zgodnie z konwencją API (CamelCase)
		public static string ToJson<T>(this T data)
		{
			return JsonSerializer.Serialize(data, _jsonOutputSettings);
		}
	}

	//Pomocnicze: rozszerzenie maskujące wartości pól w raportowanych strukturach JSON, które mogą być poufne
	public static class LoggerExtensions
	{

		//Pomocnicza funkcja, maskująca zawartość wskazanego pola w teksćie JSON (na potrzeby loga)
		private const int MASK_BORDER_LENGTH = 5;
		private const string MASK_FIELD = "(...)";
		private const string MASK_DATA = "{...}";
		private const string RESTRICTED_REQUESTS = "#EncodeData, #DecodeData, #FromBase64, #ToBase64"; //Danych tych żądań w ogóle nie pokazuj
		private readonly static string[] _maskedFields = ["privateKeyPem","token", "certificatePem"];
		//Odnotowuje w logu dane przekazane wraz z żądaniem (tylko gdy logLevel jest Debug lub Trace)
		//Argumenty:
		//	request:	żądanie
		//	json:		tekst otrzymanych danych (struktura JSON)
		public static void LogRequestData(this ILogger logger, string request, string? json)
		{
			if(!logger.IsEnabled(LogLevel.Debug)) return;
			if (json == null) return;
			json = json.Replace("\n", "\n\t");
			if (RESTRICTED_REQUESTS.Contains(request, StringComparison.OrdinalIgnoreCase)) 
				json = MASK_DATA;
			else
				MaskRestrictedFields(ref json);

			logger.LogDebug("Received data accompanying request '{request}':\n\n\t{data}\n", request, json);
		}

		//Odnotowuje w logu rezultat żądania (tylko gdy logLevel jest Debug lub Trace)
		//Argumenty:
		//	request:	żądanie
		//	json:		tekst zwracanych danych (struktura JSON)
		public static void LogReturnedData(this ILogger logger, string request, string? json)
		{
			if (!logger.IsEnabled(LogLevel.Debug)) return;
			if (json == null) return;
			json = json.Replace("\n", "\n\t");
			if (RESTRICTED_REQUESTS.Contains(request, StringComparison.OrdinalIgnoreCase)) 
				json = MASK_DATA;
			else
				MaskRestrictedFields(ref json);

			logger.LogDebug("Sending (processed) result to the caller:\n\n\t{result}\n", json);
		}
		//Pomocnicze: maskuje w tekście zastrzeżone pola
		//Argumenty:
		//	json: tekst do ewentualnej modyfikacji.
		public static void MaskRestrictedFields(ref string json)
		{
			foreach (var field in _maskedFields) 	
					MaskFieldValue(ref json, field);
		}
		//Argumenty:
		//	json:			tekst struktury JSON
		//	fieldName:		maskowane pole. Zakładam, że to wartość Base64, która nie będzie zawierała cudzysłowu jako znaku specjalnego
		//Zwraca true, gdy tekst json został zmodyfikowany, w przeciwnym razie - false.
		private static bool MaskFieldValue(ref string json, string fieldName)
		{
			int i = 0, i1, i2;
			bool ret = false;
			do //fieldName może wystąpić w tekście więcej niż raz, stąd poniższe kroki są w pętli
			{
				i = json.IndexOf(fieldName, i);
				if (i < 0) return ret;
				i = json.IndexOf(':', i);
				if (i < 0) return ret;
				i1 = json.IndexOf('"', i); //cudzysłów otwierający
				if (i1 < 0) return ret;  //cudzysłów zamykający
				i2 = json.IndexOf('"', i1 + 1);
				if (i2 < 0) return ret;
				ret |= true;
				var result = new StringBuilder();
				result.Append(json.AsSpan(0, i1 + 1)); //Wstaw początek - do cudzysłowu (włącznie)
				var len = i2 - i1 - 1;
				if (len <= (2 * MASK_BORDER_LENGTH))
				{
					result.Append(MASK_FIELD);
				}
				else
				{
					result.Append(json.AsSpan(i1 + 1, MASK_BORDER_LENGTH));
					result.Append(MASK_FIELD);
					result.Append(json.AsSpan(i2 - MASK_BORDER_LENGTH, MASK_BORDER_LENGTH));
				}
				result.Append(json.AsSpan(i2));
				json = result.ToString();

			} while (i <  json.Length);
			return ret;
		}
	}

}
