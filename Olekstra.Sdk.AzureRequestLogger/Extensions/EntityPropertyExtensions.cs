namespace Microsoft.Azure.Cosmos.Table
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    internal static class EntityPropertyExtensions
    {
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            IgnoreNullValues = true,
            IgnoreReadOnlyProperties = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = false,
        };

        internal static EntityProperty? TryGet(this IDictionary<string, EntityProperty> properties, string name)
        {
            properties = properties ?? throw new ArgumentNullException(nameof(properties));

            properties.TryGetValue(name, out var prop);
            return prop;
        }

        internal static void Set(this IDictionary<string, EntityProperty> properties, string name, string value)
        {
            properties = properties ?? throw new ArgumentNullException(nameof(properties));

            if (!string.IsNullOrEmpty(value))
            {
                properties[name] = new EntityProperty(value);
            }
        }

        internal static T? TryGetDeserialized<T>(this IDictionary<string, EntityProperty> properties, string name)
            where T : class
        {
            properties = properties ?? throw new ArgumentNullException(nameof(properties));

            if (!properties.TryGetValue(name, out var prop))
            {
                return default;
            }

            var val = prop.StringValue;

            if (string.IsNullOrEmpty(val))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(val, JsonOptions);
        }

        internal static void SetSerialized<T>(this IDictionary<string, EntityProperty> properties, string name, T value)
            where T : class
        {
            properties = properties ?? throw new ArgumentNullException(nameof(properties));

            if (value != null)
            {
                properties.Add(name, new EntityProperty(JsonSerializer.Serialize(value, JsonOptions)));
            }
        }
    }
}
