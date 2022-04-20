namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Collections.Generic;
    using Azure.Data.Tables;

    public class LogEntity
    {
        private const string AdditionalValuePrefix = "X_";

        private Dictionary<string, string>? additionalValues = null;

        public LogEntity(string method, string path, DateTimeOffset requestTime)
        {
            this.Method = method;
            this.Path = path;
            this.RequestTime = requestTime;

            this.PartitionKey = path.Trim('/');
            this.RowKey = requestTime.GetInvertedTicks();
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }

        public DateTimeOffset RequestTime { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }

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

        public TableEntity CreateTableEntity(char keySanitizationReplacement)
        {
            var spk = LogService.SanitizeKeyValue(PartitionKey, keySanitizationReplacement);
            var srk = LogService.SanitizeKeyValue(RowKey, keySanitizationReplacement);

            var te = new TableEntity(spk, srk)
            {
                [nameof(RequestTime)] = RequestTime,
                [nameof(Method)] = Method,
                [nameof(Path)] = Path,
                [nameof(Query)] = Query,
                [nameof(StatusCode)] = StatusCode,
                [nameof(TotalMilliseconds)] = TotalMilliseconds,

                [nameof(RequestBody)] = RequestBody,
                [nameof(ResponseBody)] = ResponseBody,
                [nameof(RequestBodyLength)] = RequestBodyLength,
                [nameof(ResponseBodyLength)] = ResponseBodyLength,

                [nameof(Exception)] = Exception,
                [nameof(IP)] = IP,
            };

            if (RequestBodyTruncated)
            {
                te[nameof(RequestBodyTruncated)] = RequestBodyTruncated;
            }

            if (ResponseBodyTruncated)
            {
                te[nameof(ResponseBodyTruncated)] = ResponseBodyTruncated;
            }

            if (additionalValues != null)
            {
                foreach (var kv in additionalValues)
                {
                    if (!string.IsNullOrEmpty(kv.Value))
                    {
                        te.Add(AdditionalValuePrefix + kv.Key, kv.Value);
                    }
                }
            }

            if (Attachments != null)
            {
                te.SetSerialized(nameof(Attachments), Attachments);
            }

            return te;
        }
    }
}
