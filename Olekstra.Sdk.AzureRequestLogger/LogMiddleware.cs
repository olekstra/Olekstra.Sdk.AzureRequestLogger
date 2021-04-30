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
        private readonly List<Func<PathString, bool>> pathFunctions;
        private readonly bool useAttachments;
        private readonly int bodyLengthLimit;
        private readonly char keySanitizationReplacement;
        private readonly bool autosaveAttachments;
        private readonly string requestBodyAttachmentName;
        private readonly string responseBodyAttachmentName;
        private readonly ILogger logger;

        public LogMiddleware(RequestDelegate next, LogOptions options, ILoggerFactory loggerFactory)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.logService = new LogService(options, loggerFactory.CreateLogger<LogService>());
            this.paths = options.Paths;
            this.pathFunctions = options.PathFunctions;
            this.useAttachments = options.UseAttachments;
            this.bodyLengthLimit = options.BodyLengthLimit;
            this.keySanitizationReplacement = options.KeySanitizationReplacement;
            this.autosaveAttachments = options.AutosaveBodyAttachments;
            this.requestBodyAttachmentName = options.RequestBodyAttachmentName;
            this.responseBodyAttachmentName = options.ResponseBodyAttachmentName;
            this.logger = loggerFactory.CreateLogger<LogMiddleware>();
        }

        public Task InvokeAsync(HttpContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));
            var path = context.Request.Path;

            if (paths.Any(p => path.StartsWithSegments(p, StringComparison.InvariantCultureIgnoreCase))
                || pathFunctions.Any(f => f(path)))
            {
                return DoLogging(context);
            }

            return next(context);
        }

        public async Task DoLogging(HttpContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));

            var request = context.Request;

            var logEntity = new LogEntity(request.Path.ToString(), DateTimeOffset.UtcNow)
            {
                Method = request.Method,
                Query = request.QueryString.HasValue ? request.QueryString.ToString() : default,
                IP = context.Connection.RemoteIpAddress.ToString(),
            };

            AzureRequestLoggerFeature? logFeature = null;
            Lazy<Dictionary<string, MemoryStream>>? lazyAttachments = null;
            if (context.Features.IsReadOnly)
            {
                logger.LogDebug($"HttpContext.Features.IsReadOnly={context.Features.IsReadOnly}, will not use own feature.");
            }
            else
            {
                lazyAttachments = useAttachments ? new Lazy<Dictionary<string, MemoryStream>>() : default;
                logFeature = new AzureRequestLoggerFeature(logEntity, lazyAttachments);
                context.Features.Set(logFeature);
            }

            var sw = Stopwatch.StartNew();

            if (request.Body != null && bodyLengthLimit != 0)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, leaveOpen: true);
                logEntity.RequestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                logEntity.RequestBodyLength = logEntity.RequestBody.Length;
                if (bodyLengthLimit > 0 && logEntity.RequestBodyLength > bodyLengthLimit)
                {
                    logEntity.RequestBody = logEntity.RequestBody.Substring(0, bodyLengthLimit);
                    logEntity.RequestBodyTruncated = true;

                    if (autosaveAttachments && logFeature != null && logFeature.AttachmentsEnabled)
                    {
                        await logFeature.SaveAttachmentAsync(request.Body, requestBodyAttachmentName).ConfigureAwait(false);
                    }
                }

                request.Body.Position = 0;
            }

            var elapsed = TimeSpan.Zero;

            var originalResponseBody = context.Response.Body;
            if (bodyLengthLimit != 0)
            {
                context.Response.Body = new MemoryStream();
            }

            try
            {
                await next(context).ConfigureAwait(false);
                logEntity.StatusCode = context.Response.StatusCode;
            }
            catch (Exception ex)
            {
                logEntity.Exception = ex.ToString();
                logEntity.StatusCode = StatusCodes.Status500InternalServerError;
                throw;
            }
            finally
            {
                logEntity.TotalMilliseconds = (long)sw.Elapsed.TotalMilliseconds;

                if (bodyLengthLimit != 0)
                {
                    var ms = (MemoryStream)context.Response.Body;
                    ms.Position = 0;
                    using var reader = new StreamReader(ms, leaveOpen: true);
                    logEntity.ResponseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                    logEntity.ResponseBodyLength = logEntity.ResponseBody.Length;

                    if (bodyLengthLimit > 0 && logEntity.ResponseBodyLength > bodyLengthLimit)
                    {
                        logEntity.ResponseBody = logEntity.ResponseBody.Substring(0, bodyLengthLimit);
                        logEntity.ResponseBodyTruncated = true;

                        if (autosaveAttachments && logFeature != null && logFeature.AttachmentsEnabled)
                        {
                            await logFeature.SaveAttachmentAsync(ms, responseBodyAttachmentName).ConfigureAwait(false);
                        }
                    }

                    context.Response.Body = originalResponseBody;

                    ms.Position = 0;
                    await ms.CopyToAsync(originalResponseBody).ConfigureAwait(false);
                    await ms.DisposeAsync();
                }

                if (lazyAttachments != null && lazyAttachments.IsValueCreated)
                {
                    var pk = LogService.SanitizeKeyValue(logEntity.PartitionKey, keySanitizationReplacement);
                    var rk = LogService.SanitizeKeyValue(logEntity.RowKey, keySanitizationReplacement);

                    foreach (var pair in lazyAttachments.Value)
                    {
                        logService.Attach(pair.Value, $"{pk}/{rk}/{pair.Key}");
                    }

                    logEntity.Attachments = lazyAttachments.Value.Keys.ToList();
                }

                logService.Log(logEntity);
            }
        }
    }
}
