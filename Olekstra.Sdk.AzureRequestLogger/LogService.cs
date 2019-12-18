namespace Olekstra.Sdk.AzureRequestLogger
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Cosmos.Table;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class LogService
    {
        private const int MaxBatch = 100;

        private readonly LogOptions options;

        private readonly CloudTableClient cloudTableClient;

        private readonly ILogger logger;

        private readonly ConcurrentBag<LogEntity> logEntities = new ConcurrentBag<LogEntity>();

        private readonly Task saveLogTask;

        private CloudTable? cloudTable;

        public LogService(LogOptions options, ILogger<LogService> logger)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            var storageAccount = CloudStorageAccount.Parse(options.ConnectionString);
            cloudTableClient = storageAccount.CreateCloudTableClient();

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
                if (logEntities.Count == 0)
                {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    logger.LogTrace("Nothing to save, sleeping again.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                }
                else
                {
                    try
                    {
                        var tableNameWithSuffix = BuildTableName();
                        if (cloudTable == null || cloudTable.Name != tableNameWithSuffix)
                        {
                            cloudTable = cloudTableClient.GetTableReference(tableNameWithSuffix);
                            var tableCreated = await cloudTable.CreateIfNotExistsAsync().ConfigureAwait(false);
                            logger.LogInformation($"Switched to table {tableNameWithSuffix} (created = {tableCreated})");
                        }

                        var entities = new List<LogEntity>(MaxBatch);
                        for (var i = 0; i < MaxBatch; i++)
                        {
                            if (logEntities.TryTake(out var item))
                            {
                                item.PartitionKey = SanitizeKeyValue(item.PartitionKey, options.KeySanitizationReplacement);
                                item.RowKey = SanitizeKeyValue(item.RowKey, options.KeySanitizationReplacement);

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
                            TableBatchOperation batch = new TableBatchOperation();
                            foreach (var entity in entityGroup)
                            {
                                batch.Add(TableOperation.Insert(entity));
                            }

                            await cloudTable.ExecuteBatchAsync(batch).ConfigureAwait(false);
                            groupCount++;
                        }

                        logger.LogDebug($"Saved {entities.Count} entities into Azure Table in {groupCount} batches");
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.Message + "\r\n" + e.StackTrace);
                    }
                }

                await Task.Delay(options.Interval).ConfigureAwait(false);
            }
        }
    }
}
