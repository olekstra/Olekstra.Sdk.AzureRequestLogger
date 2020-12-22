namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class AzureRequestLoggerFeature
    {
        private readonly Lazy<Dictionary<string, MemoryStream>>? attachments;

        public AzureRequestLoggerFeature(LogEntity logEntity, Lazy<Dictionary<string, MemoryStream>>? attachmentStorage)
        {
            this.LogEntity = logEntity ?? throw new ArgumentNullException(nameof(logEntity));
            this.attachments = attachmentStorage;
        }

        public LogEntity LogEntity { get; }

        public bool AttachmentsEnabled => (attachments != null);

        public async Task SaveAttachmentAsync(Stream body, string name)
        {
            if (attachments == null)
            {
                throw new InvalidOperationException("Attachments not enabled");
            }

            body = body ?? throw new ArgumentNullException(nameof(body));
            name = name ?? throw new ArgumentNullException(nameof(name));

            var ms = new MemoryStream();
            body.Position = 0;
            await body.CopyToAsync(ms).ConfigureAwait(false);
            body.Position = 0;
            attachments.Value[name] = ms;
        }

        public Task SaveAttachmentAsync(byte[] body, string name)
        {
            if (attachments == null)
            {
                throw new InvalidOperationException("Attachments not enabled");
            }

            body = body ?? throw new ArgumentNullException(nameof(body));
            name = name ?? throw new ArgumentNullException(nameof(name));

            var ms = new MemoryStream(body);
            attachments.Value[name] = ms;

            return Task.CompletedTask;
        }
    }
}
