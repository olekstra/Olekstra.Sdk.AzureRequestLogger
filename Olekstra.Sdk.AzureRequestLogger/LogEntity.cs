namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Table;

    public class LogEntity : ITableEntity
    {
        private const string AdditionalValuePrefix = "X_";

        private Dictionary<string, string>? additionalValues = null;

        public LogEntity(string path, DateTimeOffset requestTime)
            : this(path?.Trim('/'), requestTime.GetInvertedTicks())
        {
            this.Path = path;
            this.RequestTime = requestTime;
        }

        public LogEntity(string partitionKey, string rowKey)
        {
            this.PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            this.RowKey = rowKey ?? throw new ArgumentNullException(nameof(rowKey));
            this.ETag = string.Empty;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string ETag { get; set; }

        public DateTimeOffset RequestTime { get; private set; }
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? Query { get; set; }
        public int StatusCode { get; set; }
        public long TotalMilliseconds { get; set; }
        public string? RequestBody { get; set; }
        public string? ResponseBody { get; set; }
        public long RequestBodyLength { get; set; }
        public long ResponseBodyLength { get; set; }
        public bool RequestBodyTruncated { get; set; }
        public bool ResponseBodyTruncated { get; set; }
        public string? Exception { get; set; }
        public string? IP { get; set; }

#pragma warning disable CA2227 // Making this read-only will need to always initialize it with empty list, but it will be unused in 99%
        public List<string>? Attachments { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        public Dictionary<string, string> AdditionalValues
        {
            get
            {
                if (additionalValues == null)
                {
                    additionalValues = new Dictionary<string, string>();
                }

                return additionalValues;
            }

            private set
            {
                additionalValues = value;
            }
        }

        /*
         * Override and manually read/write values is faster than using reflection
         */
        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            properties = properties ?? throw new ArgumentNullException(nameof(properties));

#pragma warning disable CS8629 // Nullable value type may be null.
            RequestTime = properties[nameof(RequestTime)].DateTimeOffsetValue.Value;
            Method = properties.TryGet(nameof(Method))?.StringValue;
            Path = properties.TryGet(nameof(Path))?.StringValue;
            Query = properties.TryGet(nameof(Query))?.StringValue;
            StatusCode = properties[nameof(StatusCode)].Int32Value.Value;
            TotalMilliseconds = properties[nameof(TotalMilliseconds)].Int64Value.Value;

            RequestBody = properties.TryGet(nameof(RequestBody))?.StringValue;
            ResponseBody = properties.TryGet(nameof(ResponseBody))?.StringValue;
            RequestBodyLength = properties[nameof(RequestBodyLength)].Int32Value.Value;
            ResponseBodyLength = properties[nameof(ResponseBodyLength)].Int32Value.Value;

            RequestBodyTruncated = properties.TryGet(nameof(RequestBodyTruncated))?.BooleanValue ?? false;
            ResponseBodyTruncated = properties.TryGet(nameof(ResponseBodyTruncated))?.BooleanValue ?? false;

            Exception = properties.TryGet(nameof(Exception))?.StringValue;
            IP = properties.TryGet(nameof(IP))?.StringValue;
#pragma warning restore CS8629 // Nullable value type may be null.

            foreach(var p in properties.Where(x => x.Key.StartsWith(AdditionalValuePrefix, StringComparison.Ordinal)))
            {
                AdditionalValues.Add(p.Key.Substring(AdditionalValuePrefix.Length), p.Value.StringValue);
            }

            Attachments = properties.TryGetDeserialized<List<string>>(nameof(Attachments));
        }

        /*
         * Override and manually read/write values is faster than using reflection
         */
        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var dic = new Dictionary<string, EntityProperty>
            {
                [nameof(RequestTime)] = new EntityProperty(RequestTime),
                [nameof(Method)] = new EntityProperty(Method),
                [nameof(Path)] = new EntityProperty(Path),
                [nameof(Query)] = new EntityProperty(Query),
                [nameof(StatusCode)] = new EntityProperty(StatusCode),
                [nameof(TotalMilliseconds)] = new EntityProperty(TotalMilliseconds),

                [nameof(RequestBody)] = new EntityProperty(RequestBody),
                [nameof(ResponseBody)] = new EntityProperty(ResponseBody),
                [nameof(RequestBodyLength)] = new EntityProperty(RequestBodyLength),
                [nameof(ResponseBodyLength)] = new EntityProperty(ResponseBodyLength),

                [nameof(Exception)] = new EntityProperty(Exception),
                [nameof(IP)] = new EntityProperty(IP)
            };

            if (RequestBodyTruncated)
            {
                dic[nameof(RequestBodyTruncated)] = new EntityProperty(RequestBodyTruncated);
            }

            if (ResponseBodyTruncated)
            {
                dic[nameof(ResponseBodyTruncated)] = new EntityProperty(ResponseBodyTruncated);
            }

            if (additionalValues != null)
            {
                foreach(var kv in additionalValues)
                {
                    if (!string.IsNullOrEmpty(kv.Value))
                    {
                        dic.Add(AdditionalValuePrefix + kv.Key, new EntityProperty(kv.Value));
                    }
                }
            }

            if (Attachments != null)
            {
                dic.SetSerialized(nameof(Attachments), Attachments);
            }

            return dic;
        }
    }
}
