namespace Microsoft.AspNetCore.Builder
{
    using System;
    using Olekstra.Sdk.AzureRequestLogger;

    public static class AzureRequestLoggerExtensions
    {
        public static IApplicationBuilder UseAzureRequestLogger(this IApplicationBuilder builder, LogOptions options)
        {
            builder.UseMiddleware<LogMiddleware>(options);
            return builder;
        }

        public static IApplicationBuilder UseAzureRequestLogger(this IApplicationBuilder builder, Action<LogOptions> optionsBuilder)
        {
            var options = new LogOptions();
            optionsBuilder?.Invoke(options);

            return UseAzureRequestLogger(builder, options);
        }
    }
}
