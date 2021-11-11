namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Data.Tables;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Microsoft.Extensions.Logging;

    public class LogService
    {
        private const int MaxBatch = 100;

        private readonly LogOptions options;

        private readonly ILogger logger;

        private readonly ConcurrentBag<LogEntity> logEntities = new ConcurrentBag<LogEntity>();

        private readonly ConcurrentBag<(MemoryStream content, string name)> attachments = new ConcurrentBag<(MemoryStream, string)>();

        private readonly Task saveLogTask;

        private TableClient? cloudTable;

        private BlobContainerClient? cloudBlobContainer;

        public LogService(LogOptions options, ILogger<LogService> logger)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            this.logger = logger;

            this.saveLogTask = Task.Run(() => SaveLoop());
        }

        public static string SanitizeKeyValue(string value, char replacement)
        {
            value = value ?? throw new ArgumentNullException(nameof(value));

            var valid = value.Select(x => x switch
            {
                '/' => replacement,
                '\\' => replacement,
                '#' => replacement,
                '?' => replacement,
                '+' => replacement,
                _ when x <= '\u001F' => replacement,
                _ when x >= '\u007F' && x <= '\u009F' => replacement,
                _ => x,
            });

            return new string(valid.ToArray());
        }

        public void Log(LogEntity logEntity)
        {
            logEntity = logEntity ?? throw new ArgumentNullException(nameof(logEntity));

            logEntities.Add(logEntity);
        }

        public void Attach(MemoryStream stream, string name)
        {
            stream = stream ?? throw new ArgumentNullException(nameof(stream));

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            attachments.Add((stream, name));
        }

        private string BuildTableName()
        {
            var now = DateTimeOffset.UtcNow;

            return options.TableNameSuffixMode switch
            {
                TableNameSuffixMode.Year => options.TableName + now.Year,
                TableNameSuffixMode.YearAndQuarter => options.TableName + now.Year + "q" + now.GetQuarter(),
                TableNameSuffixMode.YearAndMonth => options.TableName + now.Year + "m" + now.Month,
                _ => options.TableName,
            };
        }

        private async Task SaveLoop()
        {
            logger.LogDebug($"Started (in SaveLoop) with table {options.TableName} and suffix mode {options.TableNameSuffixMode}");

            while (true)
            {
                try
                {
                    await SaveLogEntriesAsync().ConfigureAwait(false);
                    await SaveAttachmentsAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message + "\r\n" + e.StackTrace);
                }

                await Task.Delay(options.Interval).ConfigureAwait(false);
            }
        }

        private async Task SaveLogEntriesAsync()
        {
            if (logEntities.IsEmpty)
            {
                logger.LogTrace("No logs to save");
                return;
            }

            var tableNameWithSuffix = BuildTableName();
            if (cloudTable == null || cloudTable.Name != tableNameWithSuffix)
            {
                cloudTable = new TableClient(options.ConnectionString, tableNameWithSuffix);
                var tableCreated = await cloudTable.CreateIfNotExistsAsync().ConfigureAwait(false);
                logger.LogInformation($"Switched to table {tableNameWithSuffix} (created = {tableCreated != null})");
            }

            var entities = new List<LogEntity>(MaxBatch);
            for (var i = 0; i < MaxBatch; i++)
            {
                if (logEntities.TryTake(out var item))
                {
                    entities.Add(item);
                }
                else
                {
                    break;
                }
            }

            var entitiesGroups = entities.GroupBy(x => x.PartitionKey, StringComparer.Ordinal);
            var groupCount = 0;
            foreach (var entityGroup in entitiesGroups)
            {
                var batch = new List<TableTransactionAction>();
                batch.AddRange(entityGroup.Select(x => new TableTransactionAction(TableTransactionActionType.Add, x.CreateTableEntity(options.KeySanitizationReplacement))));
                await cloudTable.SubmitTransactionAsync(batch).ConfigureAwait(false);
                groupCount++;
            }

            logger.LogDebug($"Saved {entities.Count} entities into Azure Table in {groupCount} batches");
        }

        private async Task SaveAttachmentsAsync()
        {
            if (attachments.IsEmpty)
            {
                logger.LogTrace("No attachments to save");
                return;
            }

#pragma warning disable CA1308 // Container names must be lowercase! https://docs.microsoft.com/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata
            var tableNameWithSuffix = BuildTableName().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            if (cloudBlobContainer == null || cloudBlobContainer.Name != tableNameWithSuffix)
            {
                cloudBlobContainer = new BlobContainerClient(options.ConnectionString, tableNameWithSuffix);
                var containerCreated = await cloudBlobContainer.CreateIfNotExistsAsync().ConfigureAwait(false);
                logger.LogInformation($"Switched to container {tableNameWithSuffix} (created = {containerCreated != null})");
            }

            var count = 0;
            while(count < MaxBatch)
            {
                if (!attachments.TryTake(out var item))
                {
                    break;
                }

                count++;

                using var originalStream = item.content;
                originalStream.Position = 0;

                using var compressedStream = new MemoryStream();
                using var zip = new GZipStream(compressedStream, CompressionLevel.Optimal);
                await originalStream.CopyToAsync(zip).ConfigureAwait(false);
                await zip.FlushAsync().ConfigureAwait(false);

                compressedStream.Position = 0;

                var headers = new BlobHttpHeaders
                {
                    ContentEncoding = "gzip",
                };

                var blob = cloudBlobContainer.GetBlobClient(item.name);
                await blob.UploadAsync(compressedStream, headers).ConfigureAwait(false);
                logger.LogDebug($"Saved {item.name} ({originalStream.Length} bytes compressed to {compressedStream.Length} bytes)");
            }

            logger.LogDebug($"Saved {count} attachments into Azure Storage");
        }
    }
}
