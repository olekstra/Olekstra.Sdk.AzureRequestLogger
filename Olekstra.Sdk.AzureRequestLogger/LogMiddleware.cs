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

        public LogMiddleware(RequestDelegate next, LogOptions options, ILoggerFactory loggerFactory)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.logService = new LogService(options, loggerFactory.CreateLogger<LogService>());
            this.paths = options.Paths;
            this.bodyLengthLimit = options.BodyLengthLimit;
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

            string? requestBody = default;
            long requestLength = 0;

            if (request.Body != null)
            {
                using var reader = new StreamReader(request.Body, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                request.Body.Position = 0;
                requestLength = request.Body.Length;

                if (requestBody.Length > bodyLengthLimit)
                {
                    requestBody = "(TRUNCATED) " + requestBody.Substring(0, bodyLengthLimit);
                }
            }

            var method = request.Method;
            var path = request.Path;
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.ToString() : default;

            string? exception = default;
            var ip = context.Connection.RemoteIpAddress.ToString();

            var elapsed = TimeSpan.Zero;

            var originalResponseBody = context.Response.Body;
            context.Response.Body = new MemoryStream();

            try
            {
                var sw = new Stopwatch();
                await next(context).ConfigureAwait(false);
                elapsed = sw.Elapsed;
            }
            catch (Exception ex)
            {
                exception = ex.ToString();
                throw;
            }
            finally
            {
                var response = context.Response;
                var responseStatusCode = response.StatusCode;

                var ms = (MemoryStream)response.Body;
                ms.Position = 0;
                using var reader = new StreamReader(ms, leaveOpen: true);
                var responseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                var responseLength = ms.Length;

                if (responseBody.Length > bodyLengthLimit)
                {
                    responseBody = "(TRUNCATED) " + responseBody.Substring(0, bodyLengthLimit);
                }

                context.Response.Body = originalResponseBody;

                ms.Position = 0;
                await ms.CopyToAsync(originalResponseBody).ConfigureAwait(false);
                await ms.DisposeAsync();

                logService.Log(requestBody, responseBody, method, path, query, requestLength, responseLength, responseStatusCode, elapsed, exception, ip);
            }

            var responseStream = context.Response.Body;
            var responseBuffer = new MemoryStream();
            context.Response.Body = responseBuffer;
        }
    }
}
