﻿namespace Olekstra.Sdk.AzureRequestLogger
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http;

    public class LogOptions
    {
        public string ConnectionString { get; set; } = "UseDevelopmentStorage=true;";

        public string TableName { get; set; } = "AzureRequestLogger";

        public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

        public TableNameSuffixMode TableNameSuffixMode { get; set; } = TableNameSuffixMode.YearAndQuarter;

        public List<PathString> Paths { get; } = new List<PathString>();

        public List<Func<PathString, bool>> PathFunctions { get; } = new List<Func<PathString, bool>>();

        public int BodyLengthLimit { get; set; } = 10_000;

        public char KeySanitizationReplacement { get; set; } = '_';

        public bool UseAttachments { get; set; } = true;

        public bool AutosaveBodyAttachments { get; set; } = false;

        public string RequestBodyAttachmentName { get; set; } = "body_request.dat";

        public string ResponseBodyAttachmentName { get; set; } = "body_response.dat";

        /// <summary>
        /// Adds path to <see cref="Paths"/> list.
        /// </summary>
        /// <param name="path">Path to add.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions For(PathString path)
        {
            this.Paths.Add(path);
            return this;
        }

        /// <summary>
        /// Adds path function to <see cref="PathFunctions"/> list.
        /// </summary>
        /// <param name="pathFunction">Path function to add.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions For(Func<PathString, bool> pathFunction)
        {
            this.PathFunctions.Add(pathFunction ?? throw new ArgumentNullException(nameof(pathFunction)));
            return this;
        }

        /// <summary>
        /// Set <see cref="ConnectionString"/> property.
        /// </summary>
        /// <param name="connectionString">Azure Table Storage connection string.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions UsingConnectionString(string connectionString)
        {
            this.ConnectionString = connectionString;
            return this;
        }

        /// <summary>
        /// Set <see cref="TableName"/> and <see cref="TableNameSuffixMode"/> properties.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="mode"><see cref="TableNameSuffixMode"/> value.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions IntoTable(string tableName, TableNameSuffixMode mode)
        {
            this.TableName = tableName;
            this.TableNameSuffixMode = mode;
            return this;
        }

        /// <summary>
        /// Set <see cref="Interval"/> property.
        /// </summary>
        /// <param name="interval">Value to set.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions Every(TimeSpan interval)
        {
            this.Interval = interval;
            return this;
        }

        /// <summary>
        /// Set <see cref="BodyLengthLimit"/> property.
        /// </summary>
        /// <param name="sizeInBytes">Value to set.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions MaxBody(int sizeInBytes)
        {
            this.BodyLengthLimit = sizeInBytes;
            return this;
        }

        /// <summary>
        /// Set <see cref="UseAttachments"/> property.
        /// </summary>
        /// <param name="value">Value to set.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions Attachments(bool value)
        {
            this.UseAttachments = value;
            return this;
        }

        /// <summary>
        /// Set <see cref="AutosaveBodyAttachments"/> property.
        /// </summary>
        /// <param name="value">Value to set.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions AutosaveBody(bool value = true)
        {
            this.AutosaveBodyAttachments = value;
            return this;
        }

        /// <summary>
        /// Sets <see cref="AutosaveBodyAttachments"/> property to <b>true</b>, and <see cref="RequestBodyAttachmentName"/> and <see cref="ResponseBodyAttachmentName"/> properties to specified values.
        /// </summary>
        /// <param name="value">Value to set.</param>
        /// <returns>Current <see cref="LogOptions"/> object.</returns>
        public LogOptions AutosaveBody(string requestBodyName, string responseBodyName)
        {
            this.AutosaveBodyAttachments = true;
            this.RequestBodyAttachmentName = requestBodyName;
            this.ResponseBodyAttachmentName = responseBodyName;
            return this;
        }
    }
}
