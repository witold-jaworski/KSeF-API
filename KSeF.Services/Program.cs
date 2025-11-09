using System.Text;
using System.IO.Pipes;
using KSeF.Services.Api;
using KSeF.Services.Test;
using KSeF.Client.DI;

namespace KSeF.Services
{
	public class Program
    {
		private const string ScreenHeader = "KSeF.Services, version {0}. Author: Witold Jaworski, 2025.\nThis software is released under the MIT license terms.\n";

		private const string UsageDetails = @"
			Key command-line parameters:

			--PipesPrefix      unique name prefix for the pipes opened by this server (program)

			--TargetUrl        The API server. For KSeF servers use the TEST, DEMO and PROD symbols. 
			                   If ommited, the TEST server is assumed.

			Usage examples:

			Ksef.Services.exe --PipesPrefix ""8281322"" --TargetUrl DEMO

			Ksef.Services.exe --PipesPrefix ""7781455"" --TargetUrl ""https://jsonplaceholder.typicode.com""
			
			This program provides access to KSeF API via named pipes, internally handling all required advanced operations 
			related to encrypting/decrypting/hashing or PKI keys and certificates. This makes KSeF API accessible for most 
			programming languages, including scripts. For more details, see the documentation (..\Readme.md).

			Program terminated.
			";

		//Pomocinicze pole na konfiguracjê tego serwisu (u¿ywane przez Config {get;})
		private static IConfiguration? _configuration;
		private static ServicesState? _state;
		
		private const string UTF8 = "utf-8"; //dla porz¹dku, aby nie pope³niæ literówki

		//------------------------------------------------------------------------------------------------------------

		//Procedura g³ówna [jakby ktoœ nie wiedzia³ :)]
        public static void Main(string[] args)
        { 
			Console.WriteLine(String.Format(ScreenHeader.Replace('\t',' '), ProgramVersion())); //Przedstawmy siê!

#if DEBUG
			/*								WARUNKOWA DEZAKTYWACJA Debug.Assert()
			S¹dzi³em, ¿e w .NET, tak jak w innych œrodowiskach programistycznych, Assert(<false>) wywo³a w programie 
			skompilowanym z #DEBUG jakiœ wyj¹tek. Tutaj, gdy œledzisz aplikacjê w Visual Studio, Assert(<false>) 
			zachowuje siê "jak baranek" - ot, zatrzyma  wykonywanie jak breakpoint, i potem mo¿esz je kontynuowaæ. 
			Ku mojemu zaskoczeniu, w aplikacjach "linii poleceñ" których nie œledzi siê w debugerze, .NET Assert() 
			po prostu koñczy dzia³anie programu! Pokazuje wtedy tylko komunikat w konsoli. Ale w pliku logu - ju¿ nie. 
			Ta aplikacja normalnie dzia³a ukryta, wiêc przyczyna takiego wy³¹czenia nie zostaje w ¿aden sposób trwale 
			odnotowania - ot, program nagle siê wy³¹cza³ w trakcie wywo³ania.

			O ile KSeF.Services "zniesie" bez problemu ka¿dy wyj¹tek zg³oszony w obs³udze ¿¹dania, to jego niekontrolowane 
			(z punktu widzenia Klienta) zakoñczenie tworzy wiele problemów. Wywo³ania Debug.Assert() pe³ni¹ w tym kodzie 
			rolê pomocnicz¹. Dodawa³em je zazwyczaj aby wyeliminowaæ ostrze¿enia kompilatora o "mo¿liwej waroœci null". 
			Dlatego poni¿sze line dezaktywuj¹ wszystkie Debug.Assert() w konfiguracji #DEBUG, gdy program nie jest debugowany.
			 */
			if (!Debugger.IsAttached)
			{
				System.Diagnostics.Trace.Listeners.Clear();
				Console.WriteLine("Compiled as: DEBUG, running with assertions disabled\n");
			}
			else 
			{
				Console.WriteLine("Compiled as: DEBUG, running in debugged session\n");
			}
#else
			Console.WriteLine("Compiled as: RELEASE");
#endif

			var builder = Host.CreateApplicationBuilder(args); //W tym momencie builder.Configuration zawiera argumenty programu i ustawienia wczytane z appsettings.json

			Environment.ExitCode = 1; //Na pocz¹tku nale¿y pesymistycznie za³o¿yæ, ¿e koniec nie bêdzie normalny...
			builder.Logging.AddFileLogger();
			
			string targetUrl = builder.Configuration["TargetUrl"] ?? KsefEnvironmentsUris.TEST; //Url serwera API: gdy nie podano, to TEST KSeF

			//Serwisy udostêpnione przez program zale¿¹ od argumentu z url serwera:
			switch (targetUrl.ToLower())
			{
				//Serwer ToDo (zupe³nie inne polecenia)
				case "https://jsonplaceholder.typicode.com": 
					//Zarejestruj serwisy dla prostego klienta testowego:
					builder.Services.AddTestClient(targetUrl);
					break;

				//serwery API KSeF:
				case "test":
				case "local":
				case "local:test":
					builder.Services.AddApiClient(KsefEnvironmentsUris.TEST, targetUrl);
					break;
				case "demo":
				case "local:demo":
					builder.Services.AddApiClient(KsefEnvironmentsUris.DEMO, targetUrl);
					return; 
				case "prod":
				case "local:prod":
					// builder.Services.AddApiClient(KsefEnviromentsUris.PROD);
					Console.WriteLine("\nNo KSeF PROD environment (yet). \nProgram stops.");
					return;
				default:
					//Podano adres wprost - diabli wiedz¹, co to jest 
					builder.Services.AddApiClient(targetUrl,"???");
					break;
			}

			//Usuwam domyœlne logowanie ¿¹dañ HTTP, bo jest nieadekwatne dla potrzeb u¿ytkowników tej aplikacji
			//(zaimplementowa³em w³asne - por. klasa HttpRequestObserver, wykorzystywana przez serwis Worker)
			builder.Services.ConfigureHttpClientDefaults(configure => configure.RemoveAllLoggers());
			
			//Wstawiamy nasze serwisy: 
			builder.Services.AddHostedService<Worker>();
			builder.Services.AddHostedService<Indicator>();

			//Tworzymy hosta (w tym momencie s¹ tworzone instancje loggerów itp.)
			var host = builder.Build(); 

			//Odczytujemy konfiguracjê (udostêpniana serwisom poprzez statyczny Program.Config {get;})
			_configuration = host.Services.GetRequiredService<IConfiguration>();

			//Poni¿ej uzupe³niam brakuj¹ce ustawienia w konfiguracji tej aplikacji wartoœciami domyœlnymi,
			//by nie rozwa¿aæ w daszych czêœciach programu, czy jakiœ parametr istnieje, czy nie.

			//Settings: œcie¿ka do pliku z konfiguracj¹ (KSeF.Services.json). *NIE WSTAWIAM DOMYŒLNEJ WARTOŒCI DLA TEGO PARAMETRU DO _configuration*
			LoadConfigurationFile(builder, _configuration["Settings"]??"../*"); //domyœlnie (gdy nie ma "Settings"): szukaj pliku z konfiguracj¹ w folderze nadrzêdnym

			//StatusFrequency: najwiêksza czêstotliwoœæ (Hz), z jak¹ w opcjonalnym potoku diagnostycznym "*.sta" 
			//bêdzie raportowany stan serwisów hosta (np. kiedy mo¿na ju¿ odczytaæ dane). Odpowiada za to serwis Indicator.
			if (_configuration["StatusFrequency"] == null) _configuration["StatusFrequency"] = "5"; //max. czêstotliwoœæ raportowania stanu serwisów w potoku "*.sta"

			//LogsFolder: folder na pliki logów dziennych (mo¿e byæ œcie¿ka wzglêdna, wzglêdem folderu z plikiem *.exe tego programu):
			if (_configuration["LogsFolder"] == null) _configuration["LogsFolder"] = "..\\logs"; //domyœlnie program jest w \bin, a obok jest \logs

			//CertsFolder: folder na pliki certyfikatów (mo¿e byæ œcie¿ka wzglêdna, wzglêdem folderu z plikiem *.exe tego programu):
			if (_configuration["CertsFolder"] == null) _configuration["CertsFolder"] = "..\\certs"; //domyœlnie program jest w \bin, a obok jest \logs

			//CertsCheckBefore: Zacznij próbowac pobraæ nowy certyfikat z serwera na podan¹ w tym parametrze liczbê dni przed up³ywem terminu
			//publicznego certyfikatu serwera obecnie przechowywanego w CertsFolder. (Uwzglêdnia najstarszy z umieszczonych na tej liœcie)
			if (_configuration["CertsCheckBefore"] == null) _configuration["CertsCheckBefore"] = "5"; 

			//OutputEncoding: dotyczy potoków .out i .sta (program jakoœ poprawnie wykrywa enkodowanie dla danych wejœciowych w potoku .in)
			//domyœlnie .NET przyjmuje "utf-8", dlatego (wbrew przyjêtym tu konwencjom) poni¿sza linia jest niepotrzebna:
			//if (_configuration["OutputEncoding"] == null) _configuration["OutputEncoding"] = UTF8; //mo¿e byæ "windows-1250"

			//W zwi¹zku z tym, ¿e w  komunikacji poprzez potoki nie mo¿emy polegaæ na standardowych metodach EndOfStream lub podobnych,
			//ca³a wymieniana paczka danych musi technicznie stanowiæ 1 liniê. W zwi¹zku z tym nadawca (Klient lub Servwer) musi zamieniæ
			//ka¿dy znak CR i LF w przesy³anym tekœcie na ustalone znaki zastêpcze, a odbiorca musi tê zamianê odwróciæ.
			//W razie czego mo¿na te kody ustaliæ w pliku appsettings.json w sekcji "EOLReplacements":{ "CR" : <kod>, "LF": <kod> }.
			//Domyœlnie to:
			if (_configuration["EOLReplacements:CR"] == null) _configuration["EOLReplacements:CR"] = "3"; //kod ASCII znaku zastêpczego
			if (_configuration["EOLReplacements:LF"] == null) _configuration["EOLReplacements:LF"] = "2"; //kod ASCII znaku zastêpczego

			_configuration["TargetUrl"] = targetUrl; //gdy NIE podasz tego parametru w linii poleceñ - to bêdzie KSeF TEST.

			//Ten argument musi byæ podany:
			if (_configuration["PipesPrefix"] == null)
			{
				Console.WriteLine("Missing required 'PipesPrefix' argument.\n" + UsageDetails.Replace('\t', ' '));
				return; //Zwracamy ExitCode = 1.
			}

			//Nadajemy stan programowi (do raportowania przez Indicator)
			_state = new ServicesState();

			//Uruchamiamy serwisy:
			host.Run();

			Environment.ExitCode = 0; //.. a jednak uda³o siê zakoñczyæ dzia³anie w sposób kontrolowany.
        }

		//--- W klasie Program umieszczam statyczne metody i w³aœciwoœci, traktuj¹c j¹ jako bibiotekê procedur dla serwisów  ----

		//Konfiguracja programu
		public static IConfiguration Config
		{
			get
			{
				if (_configuration == null) throw new InvalidOperationException("Attempt to access program configuration, which has not been initialized (yet).");
				return _configuration;
			}
		}
		//Pomocnicza (by nie "nadziaæ" siê na exception w³aœciwoœci Config
		public static bool HasConfiguration { get { return _configuration != null; } }

		//Aktualny serwer (tylko ma³e litery)
		public static string TargetUrl 
		{ 
			get 
			{
				if (_configuration == null) return String.Empty; 
				string url = _configuration["TargetUrl"] ?? String.Empty; 
				return url.ToLower();
			} 
		}

		//Zwraca true, gdy dzia³amy z TargetUrl=Local
		//(dodana wy³¹cznie po to, by ie zrobiæ gdzieœ w kodzie literówki w tekœcie "local")
		public static bool IsRunningLocal { get { return TargetUrl.StartsWith("local"); } }

		//Stan serwisów hosta (tj. tego programu)
		public static ServicesState State
		{
			get
			{
				if (_state == null) throw new InvalidOperationException("Attempt to access program state, which has not been initialized (yet).");
				return _state;
			}
		}

		//£aduje plik konfiguracyjny KSeF.Services.json lub inny, podany w --Settings
		//(o ile jest co za³adowaæ)
		//Argumenty:
		//	path: aktualna wartoœæ parametru Settings. Mo¿e siê koñczyæ "\*", je¿eli nazwa pliku to KSeF.Services.json
		private static void LoadConfigurationFile(HostApplicationBuilder builder, string path)
		{
			string dir = Path.GetDirectoryName(path)??"."; //Gdy path == null, szukaj tego pliku w folderze z binariami.
			string fname = Path.GetFileName(path);
			if (fname == null || fname == "*") //Gdy nie podano nazwy pliku - podstaw aktualn¹ nazwê pliku wykonywalnego z rozszerzeniem .json:
				fname = $"{builder.Environment.ApplicationName}.json";
			dir = FullPath(dir);
			if (File.Exists(Path.Combine(dir, fname)))
			{
				builder.Configuration
					.SetBasePath(dir)
					.AddJsonFile(fname);
			}
		}

		//Zwraca numer wersji programu
		private static string ProgramVersion()
		{
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			System.Diagnostics.FileVersionInfo info = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location); //GetVersionInfo(assembly.CodeBase.Replace("file:///", ""))
			return (info.FileVersion ?? "???").ToString();
		}

		//Zwraca zawsze pe³n¹ œcie¿kê
		//Argumenty:
		//  path: œcie¿ka, która mo¿e byæ wzglêdna
		//Œcie¿ki wzglêdne "urealnia" od folderu programu
		static public string FullPath(string path)
		{
			path = Environment.ExpandEnvironmentVariables(path);    //Aby mo¿na by³o stosowaæ %temp%
			if (System.IO.Path.IsPathRooted(path) == false) path = AppContext.BaseDirectory + path;
			return Path.GetFullPath(path);
		}

		//Zwraca zainicjowany potok o nazwie sk³adaj¹cej siê z argumentu PipesPrefix i podanej koñcówki
		//Argumenty:
		//	suffix:			koñcówka nazwy potoku (".in", ".out", ".sta", ...).
		//					Koñcówka ".in" jest dla potoków z których serwer czyta, a ".out" - do których pisze
		//					Pozostawiam jako parametr wymagany wy³¹cznie dla wiêkszej czytelnoœci kodu (móg³bym ".in" i ".out" uzale¿niæ od <forInput>)
		//  stoppingToken:  Token, przekazywany przez IHost do "wszystkiego co asynchroniczne" (argument IWorker.ExecuteTask. przekazujê dalej)
		//	forInput:	opcjonalny. Je¿eli false (domyœlny), to potok wyjœciowy (z punktu widzenia serwera: PipeDirecton.Out)
		//  logger:		opcjonalny: Je¿eli podany: odnotuje operacjê na poziomie Debug
		// Zwracany obiekt to albo StreamReader albo StreamWriter
		public static async Task<Object> CreateNamedPipeAsync(string suffix, CancellationToken stoppingToken, bool forInput = false, ILogger? logger = null)
		{
			Object result; //Na wszelki wypadek
			string name = Config["PipesPrefix"] + suffix;
			var pipe = new NamedPipeServerStream(name, forInput ? PipeDirection.In : PipeDirection.Out);
			
			logger?.LogDebug("Waiting for connection on {dir} pipe named '{Pipe}'...", forInput ? "input" : "output", name);

				await pipe.WaitForConnectionAsync(stoppingToken);   //Czeka na skrypt: albo Set p = fso.CreateTextFile("\\.\pipe\{pipeName}.in",True,True) gdy forInput = true
																	// albo Set c = fso.OpenTextFile("\\.\pipe\{pipeName}.out",ForReading) gdy forInput = false

			logger?.LogInformation("Client connected to {dir} pipe named '{Pipe}'", forInput ? "input" : "output", name);

			if (forInput) //potok wejœciowy (do serwera)
			{
				result = new StreamReader(pipe);
			}
			else //potok wychodz¹cy (z serwera)
			{
				Encoding? encoding = CodePagesEncodingProvider.Instance.GetEncoding(Config["OutputEncoding"]??UTF8); //dla "utf-8" (UTF8) ta linia zwraca null

				var stream = (encoding == null? 
								 new StreamWriter(pipe) :			 //"utf-8" oznacza enkodowanie domyœlne (Nie wiem dlaczego, ale gdy tutaj wpisa³em Encoding.UTF8, to stream nie dzia³a³)
							     new StreamWriter(pipe, encoding)
							  );
				stream.AutoFlush = true; //W³¹cz AutoFlush, bo inaczej, gdy zaczniesz pisaæ do Klienta, ten bêdzie czeka³, a¿ jawnie wywo³asz .Flush()!
				result = stream;
			}
			return result;
		}

		//Zamyka obiekt utworzony przez CreatedNamedPipe
		//Argumenty:
		//	pipeIn:	zamykany obiekt 
		//			Przekazywany poprzez referencjê - procedura zmienia jego wartoœæ na null.
		//  logger:	opcjonalny: Je¿eli podany: odnotuje operacjê na poziomie Debug
		public static void DisposeNamedPipeReader(ref StreamReader reader, ILogger? logger)
		{
			if (reader != StreamReader.Null)
			{
				logger?.LogInformation("Closing input pipe '{name}'", reader.BaseStream.ToString());
				try //nie mia³em problemów z zamykaniem potoku wejœciowego, ale tak - na wszelki wypadek:
				{
					reader.Close();
					reader.Dispose();
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "Unexpected error while closing this input stream:");
				}
				finally
				{
					reader = StreamReader.Null;
				}
			}
		}
		//Zamyka obiekt utworzony przez CreatedNamedPipe
		//Argumenty:
		//	writer:	zamykany obiekt 
		//			Przekazywany poprzez referencjê - procedura zmienia jego wartoœæ na null.
		//  logger:	opcjonalny: Je¿eli podany: odnotuje operacjê na poziomie Debug
		public static void DisposeNamedPipeWriter(ref StreamWriter writer, ILogger? logger)
		{
			if (writer != StreamWriter.Null) 
			{ 
				logger?.LogInformation("Closing output pipe '{name}'", writer.BaseStream.ToString());
				try //Je¿eli nie odczyta³eœ ostatniej linii wystawionej przez któryœ z potoków wychodz¹czych,
					//to te protestuj¹ w tym miejscu przeciwko zamkniêciu:
				{   //Rozwi¹zanie: tu¿ przed wywo³aniem zamkniêcia potoku Out, Klient powinien wczytaæ wszystko, co mu serwer w nim wystawi³.
					writer.Close();
					writer.Dispose();
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "Unexpected error while closing this output stream:");
				}
				finally
				{  writer = StreamWriter.Null; }
			}
		}
	}
}