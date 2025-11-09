using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services
{
	//Pomocniczy serwis: w regularnych odstępach czasu (parametr "StatusFrequency") wpisuje w potoku diagnostycznym (*.sta)
	//aktualny stan hosta serwisów (por. klasa ServicesState).
	//Klient może wykorzystać ten potok do przetwarzania asynchronicznego, sprawdzając po wysłaniu polecenia do potoku *.in,
	//czy już można odczytać odpowiedź z potoku *.out (czyli czy aktualny status to ServicesState.READ).
	//(Jeżeli w potok *.out jeszcze nie zostały wpisane dane, a Klient wywołał polecenie odczytu z tego potoku, to Klient "zastyga"
	//w oczekiwaniu na odczyt. Odczytywanie stale wysyłanych "anonsów" ze strumienia diagnostycznego pozwala uniknać takiej sytuacji).
	public class Indicator(ILogger<Indicator> logger) : BackgroundService
	{
		private readonly ILogger<Indicator> _logger = logger;
		private StreamWriter _stream = StreamWriter.Null;

		protected override async Task ExecuteAsync(CancellationToken stopToken)
		{
			//Jeżeli użytkownik nie otworzy tego potoku, ta metoda przez cały czas działania programu może "wisieć" na tej pierwszej linii:
			try
			{
				_stream = (StreamWriter)await Program.CreateNamedPipeAsync(".sta", stopToken, logger: _logger);
			}
			catch (System.OperationCanceledException ex) //...a przy zamykaniu programu wyrazi zwoje niezadowolnie w postaci tego wyjątku:
			{
				_logger.LogTrace("Diagnostic pipe was never connected to, and finally signalizes this as a minor exception:\n{Message}", ex.Message);
				return;
			}
			_logger.LogDebug("Service started"); //Tu jesteś tylko raz - przy rozpoczęciu pracy
			//wait: interwał czasowy wysyłania kolejnych informacji o statusie
			TimeSpan wait = new((Int64) (10000000d * (1d / Program.Config.GetValue<double>("StatusFrequency")))); // jedn: ticks (1/100 milisekundy)
			
			while (!stopToken.IsCancellationRequested) //z tej pętli nie wychodzisz przez cały czas działania serwisu
			{
				if (Program.State.Is(ServicesState.EXIT)) break; //gdy kończymy działnie: nie raportuj, tylko wyjdź z pętli
																 //W przeciwnym razie:
				DateTime stamp = DateTime.Now; //czas wysłania tej informacji do potoku
				string status = Program.State.AsString();
				status += " was at " + stamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
				await _stream.WriteLineAsync(new StringBuilder(status), stopToken); //await skończy się, gdy Klient odczyta wpisany status
				_logger.LogTrace("Client has read status line: {status}", status); 
				DateTime cur = DateTime.Now; //To dokładny czas odczytania stanu przez Klienta
				TimeSpan diff = stamp + wait - cur; //ile pozostało czasu do wymaganego kolejnego wysłania?
				if (diff > TimeSpan.Zero) await Task.Delay((int) diff.TotalMilliseconds, stopToken); //diff jest ujemny, gdy Klient się spóźnił z odczytaniem
			}

			//tu dojdziesz tylko przy szczęśliwym zbiegu okoliczności

		}

		public override async Task StopAsync(CancellationToken stopToken)
		{
			Program.DisposeNamedPipeWriter(ref _stream, _logger);
			_logger.LogDebug("Service stopped");

			await base.StopAsync(stopToken);
		}

	}
}
