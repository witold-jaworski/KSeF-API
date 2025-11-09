using KSeF.Client.Core.Interfaces.Services;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;


namespace KSeF.Services
{
	//Podstawowy serwis: wykonuje polecenia wys³ane do strumienia *.in, i po ich zakoñczeniu zwraca rezultat w strumieniu *.out
    public class Worker(ILogger<Worker> logger, IHostApplicationLifetime hostLifetime, IServiceProvider serviceProvider,
										IServiceScopeFactory scopeFactory, ILogger<HttpRequestsObserver> httpLogger) : BackgroundService
    {
		private const string NODATA = "{}"; //Taki tekst odsy³aj Klientowi, gdy ¿¹danie nie zwróci³o ¿adnej wartoœci

        private readonly ILogger<Worker> _logger = logger;
		private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;
		private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
		private readonly ILogger<HttpRequestsObserver> _httpLogger = httpLogger; //dla "przelotnych" obiektów, œledz¹cych ¿¹dania HTTP (aby nie tworzyæ go co chwila)
		private readonly IServiceProvider _serviceProvider = serviceProvider;
		protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
			_logger?.LogInformation("Target server: '{server}', current user: '{user}'", Program.Config["TargetUrl"], Environment.UserName); //Informacyjnie

			StreamWriter pipeOut = (StreamWriter) await Program.CreateNamedPipeAsync(".out", stopToken, logger : _logger);
			//pierwsze await przekaza³o sterowanie do inicjalizacji nastêpnego serwisu (Indicator)
			StreamReader pipeIn = (StreamReader) await Program.CreateNamedPipeAsync(".in", stopToken, forInput: true, logger: _logger);
			bool canContinue = true;

			//Tu jesteœ tylko raz - przy rozpoczêciu pracy
			_logger?.LogDebug("Service started"); //Tu jesteœ tylko raz - przy rozpoczêciu pracy

			while (!stopToken.IsCancellationRequested && canContinue) //z tej pêtli nie wychodzisz przez ca³y czas dzia³ania serwisu
            { 
				Program.State.Set(ServicesState.STBY, _logger);

				var request = await pipeIn.ReadLineAsync(stopToken);
				var dataToRead = (request != null && request.EndsWith('\t')); //Tabulacja na koñcu ¿¹dania mo¿e byæ stosowana przez Klienta dzia³aj¹cego synchronicznie,
																			  //i oznacza, ¿e do wczytania s¹ jeszcze dane
				request = request?.Trim(); //OK< mo¿na usun¹æ tê ewentualn¹ spacjê
				_logger?.LogInformation("Received new request: '{request}'\n", request);

				Program.State.Set(ServicesState.NREQ, _logger, request??""); //Kod asynchroniczny lepiej debugowaæ, pozostawiaj¹c ten status jest PRZED ew. breakpointem

				using var observer = new HttpRequestsObserver(_httpLogger);
				using (DiagnosticListener.AllListeners.Subscribe(observer))
				if (request != null && request.ToUpper() != "END")
				{
					using IServiceScope scope = _scopeFactory.CreateScope();

						try
						{
							IRequestHandler processor = scope.ServiceProvider.GetRequiredService<IRequestHandlerProvider>()
																						.GetHandlerFor(request, scope.ServiceProvider);
							if (processor.RequiresInput || dataToRead)
							{
								Program.State.Set(ServicesState.WRTE, _logger, request, processor); //Bardzo wa¿ne, aby zmiana stanu by³a PRZED lini¹ odczytu:
								string? data = await pipeIn.ReadLineAsync(stopToken); dataToRead = false;
								data = data.AsMultiLine(); //"rozpakuj" w ewntualny tekst wieloliniowy
								//_logger?.LogDebug("Received data accompanying request '{request}':\n\n\t{data}\n", request, data?.Replace("\n","\n\t"));
								_logger?.LogRequestData(request, data);
								if (processor.RequiresInput) //Na wszelki wypadek, gdybœ po stronie Klienta wys³a³ coœ "przez pomy³kê"
								{
									Program.State.Set(ServicesState.PRCS, _logger, request, processor);
									await processor.PrepareInputAsync(data ?? String.Empty, stopToken);
								}
								else
									_logger?.LogDebug("(?!) Request handler declines processing this data (is it a programmer mistake?)");
							}

							Program.State.Set(ServicesState.WAIT, _logger, request, processor);
							await processor.ProcessAsync(stopToken);
							
							string? result = NODATA;
							if (processor.HasResults)
							{
								result = processor.SerializeResults();
								//_logger?.LogDebug("Sending (processed) result to the caller:\n\n\t{result}\n", result?.Replace("\n", "\n\t"));
								_logger?.LogReturnedData(request, result);
							}
							//Jakieœ dane musimy zwróciæ ZAWSZE, aby Klient móg³ dzia³aæ w trybie synchronicznym:
							Program.State.Set(ServicesState.READ, _logger, request); //Bardzo wa¿ne, aby zmiana stanu by³a PRZED lini¹ zapisu:
							
							await pipeOut.WriteLineAsync(result.AsSingleLine()); //Da³em sobie tu spokój ze stopToken, bo nie ma z nim wariantu z argumentem string

							_logger?.LogInformation("Request '{request}' completed.\n", request);
						}
						catch (Exception ex)
						{
							_logger?.LogError(ex, "Error while processing '{request}' request", request); //Odnotuj b³¹d...
																										  
							if (dataToRead) //Czy przypadkiem Klient nie "wisi" jeszcze na czymœ, co wys³a³ do wczytania? 
							{ //Jezeli tak, to wczytaj tê liniê (zapewne z danymi) - aby móg³ przejœæ do odczytu tego, co mu wpiszesz poni¿ej
								await pipeIn.ReadLineAsync(stopToken); dataToRead = false;
							}

							//Wyœlij ten wyj¹tek do Klienta:
							var status = Program.State.AsString();
							Program.State.Set(ServicesState.READ, _logger, request);
							await pipeOut.WriteLineAsync(ex.ToJson(status).AsSingleLine());

							//Podsumuj sprawê w logu:
							_logger?.LogInformation("Canceled processing request '{request}'\n", request);

							//..i zacznij kolejny obrót tej pêtli!
						}		
				}
				else
					canContinue = false;
			}

			//A tu jesteœ tylko wtedy, gdy sam zamkn¹³eœ pêtlê while.
			Program.State.Set(ServicesState.EXIT, _logger); //dla porz¹dku, aby przypadkiem Indicator nam czegoœ nie wpisa³...

			Program.DisposeNamedPipeReader (ref pipeIn, _logger);
			Program.DisposeNamedPipeWriter (ref pipeOut, _logger);

			_hostLifetime.StopApplication();

			_logger?.LogDebug("Service stopped");
		}
	}
}
