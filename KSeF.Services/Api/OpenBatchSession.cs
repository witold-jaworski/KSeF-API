using System;
using System.Collections.Generic;
using System.IO.Compression;
using KSeF.Client.Api.Builders.Batch;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Sessions;
using KSeF.Client.Core.Models.Sessions.BatchSession;
using KSeF.Client.Http;

namespace KSeF.Services.Api
{
	//Stworzenie interaktywnej sesji wysyłania faktur sprzedaży
	[HandlesRequest("OpenBatchSession")]
	internal class OpenBatchSession : HandlerBase
	{
		//------------ Struktury ------------------
		//Struktura danych wejściowych (w JSON pierwsze litery nazw pól mają być małe):
		protected class InputData
		{
			public required FormCode InvoiceFormat { get; set; } //Deklaracja formatu wysyłanych faktur
			public required string SrcFolder { get; set; } //ścieżka do foldera z plikami do wysłania
			public required List<string> Files { get; set; }    //Nazwy plików wysyłanych z SrcFolder
			public bool OfflineMode { get; set; } = false;
			public required string AccessToken { get; set; } //aktualny token dostępowy
		}

		//Struktura danych wyjściowych:
		protected class Results
		{
			public string ReferenceNumber { get; set; } = ""; //Numer referencyjny do sesji wsadowej
		}

		//Struktura danych wewnętrznych:
		protected class Params
		{
			public OpenBatchSessionRequest Request { get; set; } = new();
			public List<BatchPartSendingInfo> Parts { get; set; } = [];
			public string AccessToken { get; set; } = "";
		}
		//----------------------		
		protected const int MAX_FILES = 10000; //max. liczba plików w paczce
		protected const int MAX_PART_SIZE = 100 * 1024 * 1024; // max. rozmiar pojedynczej części paczki: 100 MB

		protected Params _params = new();
		protected Results _output = new();
		public override bool RequiresInput { get { return true; } }
		public override bool HasResults { get { return true; } }

		public override Task PrepareInputAsync(string data, CancellationToken stopToken)
		{
			var inp = JsonUtil.Deserialize<InputData>(data);
			if (inp == null) throw new ArgumentException($"Cannot parse expression '{data}'", nameof(data));

			_params.AccessToken = inp.AccessToken;

			var cryptoService = Scope.GetRequiredService<ICryptographyService>();
			var enc = cryptoService.GetEncryptionData();

			inp.SrcFolder = ValidateForInput(inp.SrcFolder,"srcFolder");

			//Stwórz w pamięci zip-a i zaszyfrowane części:
			(byte[] zipBytes, FileMetadata zipMeta) = BuildZip(inp.SrcFolder, inp.Files, cryptoService);

			int partCount = (int)Math.Ceiling((double)zipBytes.Length / MAX_PART_SIZE);

			_params.Parts = EncryptAndSplit(zipBytes, enc, cryptoService, partCount);

			var builder = OpenBatchSessionRequestBuilder.Create()
				.WithFormCode(inp.InvoiceFormat.SystemCode, inp.InvoiceFormat.SchemaVersion, inp.InvoiceFormat.Value)
				.WithOfflineMode(inp.OfflineMode)
				.WithBatchFile(fileSize: zipMeta.FileSize, fileHash: zipMeta.HashSHA);
					foreach (var p in _params.Parts)
					{
						builder = builder.AddBatchFilePart(
							ordinalNumber: p.OrdinalNumber,
							fileName: $"part_{p.OrdinalNumber}.zip.aes",
							fileSize: p.Metadata.FileSize,
							fileHash: p.Metadata.HashSHA);
					}
			_params.Request = builder.EndBatchFile()
				.WithEncryption(enc.EncryptionInfo.EncryptedSymmetricKey, enc.EncryptionInfo.InitializationVector)
				.Build();

			return Task.CompletedTask;
		}

		public override async Task ProcessAsync(CancellationToken stopToken)
		{
			Debug.Assert(_ksefClient != null);
			var response = await _ksefClient.OpenBatchSessionAsync(_params.Request, _params.AccessToken, cancellationToken: stopToken);
			//Wyślij przygotowane części zip-a:
			await _ksefClient.SendBatchPartsAsync(response,_params.Parts, stopToken);
			_output.ReferenceNumber = response.ReferenceNumber;
		}

		public override string SerializeResults()
		{
			return _output.ToJson();
		}

		//--- metody pomocnicze ---
		//Tworzy w pamięci Zip ze wskazanych plików
		//Argumenty:
		//	folderPath:		pełna ścieżka do folderu, w którym znajdują się pliki
		//	files:			lista nazw plików (w folderze <folderPath>)
		//	crypto:			sewris pomocniczy
		//Zwraca: bajty zip-a oraz jego metdatane.
		protected static (byte[] ZipBytes, FileMetadata Meta) BuildZip(string folderPath,
				IEnumerable<string> files,
				ICryptographyService crypto)
		{
			using var zipStream = new MemoryStream();
			using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true);

			foreach (var fileName in files)
			{
				var content = File.ReadAllBytes(Path.Combine(folderPath, fileName));
				var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
				using var entryStream = entry.Open();
				entryStream.Write(content);
			}

			archive.Dispose();

			var zipBytes = zipStream.ToArray();
			var meta = crypto.GetMetaData(zipBytes);

			return (zipBytes, meta);
		}

		//Dzieli bufor na określoną liczbę części o zbliżonym rozmiarze.
		//Argumenty:
		//	input:	Bufor wejściowy.
		// partCount:	Liczba części.
		// Zwraca listę buforów podzielonych na części.
		protected static List<byte[]> Split(byte[] input, int partCount)
		{
			ArgumentNullException.ThrowIfNull(input);
			if (partCount <= 0) return [];
			//ArgumentOutOfRangeException.ThrowIfNegativeOrZero(partCount);

			var result = new List<byte[]>(partCount);
			var partSize = (int)Math.Ceiling((double)input.Length / partCount);

			for (int i = 0; i < partCount; i++)
			{
				var start = i * partSize;
				var size = Math.Min(partSize, input.Length - start);
				if (size <= 0) break;

				var part = new byte[size];
				Array.Copy(input, start, part, 0, size);
				result.Add(part);
			}

			return result;
		}
		//Dzieli na części i szyfruje orzymango zipa
		//Argumenty:
		//	zipBytes:	bajty zipa do podziału
		//	encryption:	parametry szyfrowania
		//	crypto:		serwis pomocniczy
		//Zwraca listę struktur ze stworzonymi częściami
		protected static List<BatchPartSendingInfo> EncryptAndSplit(
			byte[] zipBytes,
			EncryptionData encryption,
			ICryptographyService crypto,
			int partCount = 1)
		{
			ArgumentNullException.ThrowIfNull(zipBytes);
			ArgumentNullException.ThrowIfNull(encryption);
			ArgumentNullException.ThrowIfNull(crypto);

			var rawParts = Split(zipBytes, partCount);

			var result = new List<BatchPartSendingInfo>(rawParts.Count);

			for (int i = 0; i < rawParts.Count; i++)
			{
				var encrypted = crypto.EncryptBytesWithAES256(rawParts[i], encryption.CipherKey, encryption.CipherIv);
				var meta = crypto.GetMetaData(encrypted);

				result.Add(new BatchPartSendingInfo
				{
					Data = encrypted,
					OrdinalNumber = i + 1,
					Metadata = meta
				});
			}

			return result;
		}
	}
}
