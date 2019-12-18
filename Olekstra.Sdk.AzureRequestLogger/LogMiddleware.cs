namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.IO;
    using Microsoft.Extensions.Logging;
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.Linq;

    public class LogMiddleware
    {
        private readonly RequestDelegate next;
        private readonly LogService logService;
        private readonly List<PathString> paths;
        private readonly int bodyLengthLimit;
        private readonly ILogger logger;

        public LogMiddleware(RequestDelegate next, LogOptions options, ILoggerFactory loggerFactory)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.logService = new LogService(options, loggerFactory.CreateLogger<LogService>());
            this.paths = options.Paths;
            this.bodyLengthLimit = options.BodyLengthLimit;
            this.logger = loggerFactory.CreateLogger<LogMiddleware>();
        }

        public Task InvokeAsync(HttpContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));
            var path = context.Request.Path;

            if (paths.Any(p => path.StartsWithSegments(p, StringComparison.InvariantCultureIgnoreCase)))
            {
                return DoLogging(context);
            }

            return next(context);
        }

        public async Task DoLogging(HttpContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));

            var request = context.Request;

            request.EnableBuffering();

            var logEntity = new LogEntity(request.Path.ToString(), DateTimeOffset.UtcNow)
            {
                Method = request.Method,
                Query = request.QueryString.HasValue ? request.QueryString.ToString() : default,
                IP = context.Connection.RemoteIpAddress.ToString(),
            };

            if (request.Body != null)
            {
                using var reader = new StreamReader(request.Body, leaveOpen: true);
                logEntity.RequestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                logEntity.RequestBodyLength = request.Body.Length;

                if (logEntity.RequestBodyLength > bodyLengthLimit)
                {
                    logEntity.RequestBody = "(TRUNCATED) " + logEntity.RequestBody.Substring(0, bodyLengthLimit);
                }

                request.Body.Position = 0;
            }

            var elapsed = TimeSpan.Zero;

            var originalResponseBody = context.Response.Body;
            context.Response.Body = new MemoryStream();

            var logFeature = new AzureRequestLoggerFeature(logEntity);
            if (context.Features.IsReadOnly)
            {
                logger.LogDebug($"HttpContext.Features.IsReadOnly={context.Features.IsReadOnly}, will not use own feature.");
            }
            else
            {
                context.Features.Set(logFeature);
            }

            var sw = Stopwatch.StartNew();

            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logEntity.Exception = ex.ToString();
                throw;
            }
            finally
            {
                var response = context.Response;

                logEntity.StatusCode = response.StatusCode;
                logEntity.TotalMilliseconds = (long)sw.Elapsed.TotalMilliseconds;

                var ms = (MemoryStream)response.Body;
                ms.Position = 0;
                using var reader = new StreamReader(ms, leaveOpen: true);
                logEntity.ResponseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                logEntity.ResponseBodyLength = ms.Length;

                if (logEntity.ResponseBodyLength > bodyLengthLimit)
                {
                    logEntity.ResponseBody = "(TRUNCATED) " + logEntity.ResponseBody.Substring(0, bodyLengthLimit);
                }

                context.Response.Body = originalResponseBody;

                ms.Position = 0;
                await ms.CopyToAsync(originalResponseBody).ConfigureAwait(false);
                await ms.DisposeAsync();

                logService.Log(logEntity);
            }
        }
    }
}
