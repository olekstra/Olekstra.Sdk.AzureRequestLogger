namespace Olekstra.Sdk.AzureRequestLogger
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

        public int BodyLengthLimit { get; set; } = 10_000;

        public char KeySanitizationReplacement { get; set; } = '_';

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
    }
}
