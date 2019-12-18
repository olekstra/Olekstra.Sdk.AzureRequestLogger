namespace Olekstra.Sdk.AzureRequestLogger
{
    using Microsoft.AspNetCore.Http;
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

        private readonly CloudTableClient cloudTableClient;

        private CloudTable? cloudTable;

        private readonly string tableName;

        private readonly TimeSpan interval;

        private readonly TableNameSuffixMode tableNameSuffixMode;

        private readonly ILogger logger;

        private readonly ConcurrentBag<LogEntity> logEntities = new ConcurrentBag<LogEntity>();

        private readonly Task saveLogTask;

        public LogService(LogOptions options, ILogger<LogService> logger)
        {
            options = options ?? throw new ArgumentNullException(nameof(options));

            var storageAccount = CloudStorageAccount.Parse(options.ConnectionString);
            cloudTableClient = storageAccount.CreateCloudTableClient();

            this.tableName = options.TableName;
            this.interval = options.Interval;
            this.tableNameSuffixMode = options.TableNameSuffixMode;

            this.logger = logger;

            this.saveLogTask = Task.Run(() => SaveLoop());
        }

        public void Log(DateTimeOffset requestTime, string? request, string? response, string method, PathString path, string? query, long requestLenght, long responseLenght, int statusCode, TimeSpan totalTime, string? exception, string? ip)
        {
            LogEntity entity = new LogEntity(path.ToString().Trim('/').Replace('/', '-'), requestTime);

            entity.Method = method;
            entity.Path = path;
            entity.Query = query;
            entity.StatusCode = statusCode;
            entity.TotalMilliseconds = (long)totalTime.TotalMilliseconds;

            entity.RequestBody = request;
            entity.ResponseBody = response;
            entity.RequestBodyLength = requestLenght;
            entity.ResponseBodyLength = responseLenght;

            entity.Exception = exception;
            entity.IP = ip;

            logEntities.Add(entity);
        }

        public void Log(LogEntity logEntity)
        {
            logEntities.Add(logEntity);
        }

        private string BuildTableName()
        {
            var now = DateTimeOffset.UtcNow;

            return tableNameSuffixMode switch
            {
                TableNameSuffixMode.Year => tableName + now.Year,
                TableNameSuffixMode.YearAndQuarter => tableName + now.Year + "q" + now.GetQuarter(),
                TableNameSuffixMode.YearAndMonth => tableName + now.Year + "m" + now.Month,
                _ => tableName,
            };
        }

        private async Task SaveLoop()
        {
            logger.LogDebug($"Started (in SaveLoop) with table {tableName} and suffix mode {tableNameSuffixMode}");

            while (true)
            {
                if (logEntities.Count == 0)
                {
                    logger.LogTrace("Nothing to save, sleeping again.");
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

                await Task.Delay(interval).ConfigureAwait(false);
            }
        }
    }
}
