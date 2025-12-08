using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Invoices;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Http;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KSeF.Services.Api
{
	//Pobiera z KSeF paczkę z rezultatem żądania faktur (por. SubmitInvoicesRequest, GetInvoicesRequestStatus)
	//zapisuje pliki faktur do wskazanego foldera i zwraca listę ich metadanych (max. 10 tys faktur) 
	[HandlesRequest("DownloadInvoices")]
	internal class DownloadInvoices : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required string DstFolder {  get; set; } //ścieżka do katalogu na pobrane pliki XML faktur (może być względna)
			public required CipherData Encryption { get; set; } //Dane potrzebne do odszyforwania paczek faktur (te zwrócone przez SubmitInvoicesRequest)
			public required InvoiceExportPackage Package { get; set; } //Informacje dot. pobrania paczki (zwrócone przez GetInvoicesRequestStatus)
		}

		//Struktura danych wyjściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class Results
		{
			public bool IsTruncated { get; set; } = false;
			public List<InvoiceSummary> Invoices { get; set; } = []; //Lista z opisem faktur (taka sama, jak rezultat ListSubjectInvoices)
		}

		protected InputData? _input;
		protected Results _output = new();

		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			_input = JsonUtil.Deserialize<InputData>(data);
			if (_input == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_input.DstFolder = ValidateForInput(_input.DstFolder, "fieldName"); //ForInput, bo ten folder musi istnieć
			_output.IsTruncated = _input.Package.IsTruncated;

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_input != null);
			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			EncryptionData encryption = new()
			{
				CipherKey = Convert.FromBase64String(_input.Encryption.Base64Key),
				CipherIv = Convert.FromBase64String(_input.Encryption.Base64Mix)
				//EncryptionInfo nie wypełniam, bo nie będzie wykorzystywane w metodzie wywoływanej poniżej
			};

			await DownloadAndProcessPackageAsync(_input.Package, encryption, cryptoService, stopToken);
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

		//------------------ Elementy skopiowane z KSeF.Client.Tests.Utils i .Core  --------------------

		private const string MetadataEntryName = "_metadata.json";
		private const string XmlFileExtension = ".xml";
		private static readonly JsonSerializerOptions MetadataSerializerOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
		};

		//Adaptacja metody z KSeF.Client.Test.Core/E2E/Invoice/IncrementalInvoiceRetrievalE2ETests
		//Argumenty:
		//	package:				dane zwrócone przez GetInvoicesRequestStatus
		//	encryptionData:			klucz i iv (otrzymane z SubmitInvoicesRequest)
		//	cryptographyService:	serwis biblioteki KSeF
		//	cancellationToken:		token anulowania operacji (dla porządku)
		//Metoda wykonuje właściwą obsługę tego żądania - rozpakowauje i zapisuje pliki *.XML faktur do wskazanego katalogu, wypełnia zwracaną listę faktur.
		protected async Task DownloadAndProcessPackageAsync(InvoiceExportPackage package, EncryptionData encryptionData, ICryptographyService cryptographyService, CancellationToken cancellationToken)
		{
			Debug.Assert(_input != null);

			// Pobranie, odszyfrowanie i połączenie wszystkich części w jeden strumień
			using MemoryStream decryptedArchiveStream = await DownloadAndDecryptPackagePartsAsync(
				package.Parts,
				encryptionData,
				cryptographyService,
				cancellationToken: cancellationToken);

			// Rozpakowanie ZIP
			Dictionary<string, string> unzippedFiles = await UnzipAsync(decryptedArchiveStream, cancellationToken);

			foreach ((string fileName, string content) in unzippedFiles)
			{
				if (fileName.Equals(MetadataEntryName, StringComparison.OrdinalIgnoreCase))
				{
					InvoicePackageMetadata? metadata = JsonSerializer.Deserialize<InvoicePackageMetadata>(content, MetadataSerializerOptions);
					if (metadata?.Invoices != null)
					{
						_output.Invoices.AddRange(metadata.Invoices);
					}
				}
				else if (fileName.EndsWith(XmlFileExtension, StringComparison.OrdinalIgnoreCase))
				{
					string path = Path.Combine(_input.DstFolder, fileName);
					if (!File.Exists(path)) //Nie nadpisujemy istniejącego pliku o tej samej nazwie.
					{
						File.WriteAllText(Path.Combine(_input.DstFolder, fileName), content);
					}
				}
			}
		}

		/// <summary>
		/// Pobiera pojedynczą część paczki eksportu z URL.
		/// </summary>
		/// <param name="part">Część paczki do pobrania.</param>
		/// <param name="httpClientFactory">Funkcja fabrykująca HttpClient (opcjonalnie, domyślnie tworzy nowy HttpClient).</param>
		/// <param name="cancellationToken">Token anulowania operacji.</param>
		/// <returns>Tablica bajtów zawierająca pobraną część.</returns>
		protected static async Task<byte[]> DownloadPackagePartAsync(
			InvoiceExportPackagePart part,
			Func<HttpClient>? httpClientFactory = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(part.Url))
			{
				throw new InvalidOperationException($"Missing URL for the package part #{part.OrdinalNumber}.");
			}

			using HttpClient httpClient = httpClientFactory?.Invoke() ?? new HttpClient();
			using HttpRequestMessage request = new(new HttpMethod(part.Method ?? HttpMethod.Get.Method), part.Url);
			using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsByteArrayAsync(cancellationToken);
		}
		/// <summary>
		/// Rozpakowuje archiwum ZIP ze strumienia i zwraca słownik plików (nazwa -> zawartość).
		/// </summary>
		/// <param name="zipStream">Strumień zawierający archiwum ZIP.</param>
		/// <param name="cancellationToken">Token anulowania operacji.</param>
		/// <returns>Słownik zawierający nazwy plików i ich zawartość jako string.</returns>
		protected static async Task<Dictionary<string, string>> UnzipAsync(
			Stream zipStream,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(zipStream);

			Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

			using ZipArchive archive = new(zipStream, ZipArchiveMode.Read, leaveOpen: true);

			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (string.IsNullOrWhiteSpace(entry.Name))
				{
					continue;
				}

				using Stream entryStream = entry.Open();
				using StreamReader reader = new(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
				string content = await reader.ReadToEndAsync(cancellationToken);
				files[entry.Name] = content;
			}

			return files;
		}

		/// <summary>
		/// Pobiera, deszyfruje i łączy części paczki eksportu w jeden strumień.
		/// </summary>
		/// <param name="parts">Kolekcja części paczki do pobrania i połączenia.</param>
		/// <param name="encryptionData">Dane szyfrowania używane do deszyfrowania części.</param>
		/// <param name="crypto">Serwis kryptograficzny.</param>
		/// <param name="httpClientFactory">Funkcja fabrykująca HttpClient (opcjonalnie, domyślnie tworzy nowy HttpClient).</param>
		/// <param name="cancellationToken">Token anulowania operacji.</param>
		/// <returns>Strumień zawierający odszyfrowane i połączone dane.</returns>
		protected static async Task<MemoryStream> DownloadAndDecryptPackagePartsAsync(
			IEnumerable<InvoiceExportPackagePart> parts,
			EncryptionData encryptionData,
			ICryptographyService crypto,
			Func<HttpClient>? httpClientFactory = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(parts);
			ArgumentNullException.ThrowIfNull(encryptionData);
			ArgumentNullException.ThrowIfNull(crypto);

			MemoryStream decryptedStream = new();

			try
			{
				foreach (InvoiceExportPackagePart? part in parts.OrderBy(p => p.OrdinalNumber))
				{
					byte[] encryptedBytes = await DownloadPackagePartAsync(part, httpClientFactory, cancellationToken);
					byte[] decryptedBytes = crypto.DecryptBytesWithAES256(encryptedBytes, encryptionData.CipherKey, encryptionData.CipherIv);

					await decryptedStream.WriteAsync(decryptedBytes, cancellationToken);
				}

				decryptedStream.Position = 0;
				return decryptedStream;
			}
			catch
			{
				decryptedStream.Dispose();
				throw;
			}
		}
	}
}
