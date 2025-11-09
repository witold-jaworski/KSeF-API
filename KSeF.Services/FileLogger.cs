using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services
{
	//Implementacja interfejsu ILogger dla logowania do pliku tekstowego
	public class FileLogger(string category) : ILogger
	{
		protected const string NONE = "<none>"; //Wartość przypisywana do ścieżki logów, jeżeli logger ma być wyłączony

		private readonly string _category = category;
		private DateTime _prev = DateTime.MinValue; //pomocnicze - do odnotowania, ile czasu upłynęło od poprzedniego komunikatu
		private static string _folderPath = String.Empty;   //ścieżka do folderu, w którym mają być pliki logów 
		private readonly static Lock _lock = new();       //semafor
		private static string _filePath = String.Empty; //Ścieżka do pliku logu
		protected static bool IsInitailized()
		{ 
			if (_folderPath == String.Empty) //Spróbuj zainicjować:
			{ 
				if (!Program.HasConfiguration) return false; //jeszcze nie mamy do czego się odwołać
				_folderPath = Program.Config["LogsFolder"] ?? NONE;
				if (_folderPath == NONE) return false;
				
				_folderPath = Program.FullPath(_folderPath);
				if (!Path.Exists(_folderPath)) _folderPath = NONE; //Wyłacz logowanie, jeżeli wskazany folder nie istnieje
				else //Inicjalizujemy ścieżkę do pliku logu
				{
					_filePath = Path.GetFullPath(Path.Combine(_folderPath, DateTime.Now.ToString("yyyy-MM-dd") + "_log.txt"));
					var n = Environment.NewLine;
					AppendToFile(n+n+"---------------------------- new run ------------------------------"+n+n);					
				}
			}
			return _folderPath != NONE;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull //Nieużywany w tym programie
		{
			return null; //Nic nie musimy zwalniać na końcu rozpoczętego tym poleceniem zakresu
		}

		public bool IsEnabled(LogLevel logLevel) //Nieużywany w tym programie
		{
			return true; //Możemy zapisywać wszystko
		}

		//Pomocnicza metoda formatująca szczegóły wyjątku
		//Argumenty:
		//	ex:				wyjątek
		//	detailLevel:	poziom szczegółowości logu
		//	outer:			sformatowany opis wyjątku zewnętrznego (jeżeli taki istnieje)
		//	netLevel:		poziom zagnieżdżenia formatowanego wyjątku (niezerowy dla wyjątków wewnętrznych)
		private static string ExceptionToString(Exception? ex, LogLevel detailLevel, string outer = "",  int nestLevel = 0)
		{
			if (ex == null) return outer;
			var n = Environment.NewLine;
			bool showStackTrace = true; //(detailLevel == LogLevel.Trace || detailLevel == LogLevel.Debug);
	
			string result = n + ex.GetType() + ": " + ex.Message + n;
			if (showStackTrace) result += ex.StackTrace + n;
			result = ExceptionToString(ex.InnerException, detailLevel, result, nestLevel+1);
			return result.Replace(n, n + new String('\t', nestLevel)) + outer;
		}

		//Pomocnicza: dopisuje tekst do pliku loga
		//Argumenty:
		//	content: nowy fragment tekstu
		protected static void AppendToFile(string content)
		{
			File.AppendAllText(_filePath, content);
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, 
																						Func<TState, Exception?, string> formatter)
		{
			if (formatter != null)
			{
				lock (_lock)
				{
					if (!IsInitailized()) return;   //Nie mamy nic do roboty
					var n = Environment.NewLine;
					var ctime = DateTime.Now;//Aby mi milisekundy potem nie przeskakiwały
					

					string header = logLevel == LogLevel.Information ? "Info" : logLevel.ToString();
					header += "\t" + ctime.ToString("yyyy-MM-dd HH:mm:ss.fff") + $" <{_category}>";

					//Jeżeli można - poinformuj także, ile czasu upłynęło od ostatniego wpisu w logu z tej instancji (czyli serwisu <_category>)
					if (_prev > DateTime.MinValue)	header += " after " + FormatTS(ctime - _prev);
					_prev = ctime;
					//formatter ignoruje exception (tak podano w dokumentacji Microsoft), więc musimy o nią zadabać sami:
					string exc = ExceptionToString(exception, logLevel);

					AppendToFile(header + ":" + n + "\t" + formatter(state, exception) + n + exc + n);
				}
			}
		}

		//Pomocnicza - do "ładnego" formatowania interwału czasu
		//Argumenty:
		//	sp:	interwał czasu
		private static string FormatTS(TimeSpan sp)
		{
			string result = "";

			if (sp.TotalDays > 1)
				result += $"{Math.Floor(sp.TotalDays)}d ";
			if (sp.TotalHours > 1)
				result += sp.ToString(@"hh\:mm\:ss\.fff");
			else if (sp.TotalMinutes > 1)
				result += sp.ToString(@"m\:ss\.fff");
			else if (sp.TotalSeconds > 1)
				result += sp.ToString(@"s\.fff") + "s";
			else result += sp.Milliseconds + "ms";

			return result;
		}
	}

	//Kolejna klasa, wymagana przez Hosta:
	[ProviderAlias("File")]
	public class FileLoggerProvider : ILoggerProvider
	{
		//public FileLoggerProvider()	{}

		public ILogger CreateLogger(string categoryName)
		{
			return new FileLogger(categoryName);
		}

		public void Dispose() { return; }
	}
	public static class FileLoggerExtensions
	{
		//Direct Injection tej klasy do hosta:
		public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder)
		{
			builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());
			return builder;
		}
	}

}
