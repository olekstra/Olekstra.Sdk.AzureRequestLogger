namespace Azure.Data.Tables
{
    using System;
    using System.Text.Json;

    internal static class TableEntityExtensions
    {
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            AllowTrailingCommas = true,
#if NET6_0
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
#else
            IgnoreNullValues = true,
#endif
            IgnoreReadOnlyProperties = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = false,
        };

        internal static T? TryGetDeserialized<T>(this TableEntity tableEntity, string name)
            where T : class
        {
            tableEntity = tableEntity ?? throw new ArgumentNullException(nameof(tableEntity));

            if (!tableEntity.TryGetValue(name, out var prop))
            {
                return default;
            }

            var val = prop as string;

            if (string.IsNullOrEmpty(val))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(val, JsonOptions);
        }

        internal static void SetSerialized<T>(this TableEntity tableEntity, string name, T value)
            where T : class
        {
            tableEntity = tableEntity ?? throw new ArgumentNullException(nameof(tableEntity));

            if (value != null)
            {
                tableEntity.Add(name, JsonSerializer.Serialize(value, JsonOptions));
            }
        }
    }
}
