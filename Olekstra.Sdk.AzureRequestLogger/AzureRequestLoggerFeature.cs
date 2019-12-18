namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;

    public class AzureRequestLoggerFeature
    {
        public AzureRequestLoggerFeature(LogEntity logEntity)
        {
            this.LogEntity = logEntity ?? throw new ArgumentNullException(nameof(logEntity));
        }

        public LogEntity LogEntity { get; }
    }
}
