# Olekstra.Sdk.AzureRequestLogger

Middleware that allows you to log (to Azure Storage Table) all requests (and your responses) for some path. Useful for logging your API calls.

Based on [iflight/RequestLogger.AzureTable](https://github.com/iflight/RequestLogger.AzureTable).

[![NuGet](https://img.shields.io/nuget/v/Olekstra.Sdk.AzureRequestLogger.svg?maxAge=86400&style=flat)](https://www.nuget.org/packages/Olekstra.Sdk.AzureRequestLogger/)

## Main features

* Log records are saved in separate thread (your main pipeline performance not affected) and batched;
* Request/response body text is captured;
* Very long request/response body is truncated;
* Log tables can have suffix (with year and querter or month), so you can easy erase old logs by dropping tables;
* Additional values can be stored in log table (via `HttpContext.Features`)
* You can modify `PartitionKey` and `RowKey` of log entry during request processing
* User-provided values for PartitionKey/RowKey will be sanitized with `LogOptions.KeySanitizationReplacement` character (underscore `_` by default)

## How to use

In `Startup.cs`:

```csharp
public async void Configure(IApplicationBuilder app)
{
    ...
    app.UseAzureRequestLogger(o => 
        o.For("/sample1")
         .For("/sample2/deep-path")
         .UsingConnectionString("UseDevelopmentStorage=true;")
         .IntoTable("logs", TableNameSuffixMode.YearAndQuarter)
         .Every(TimeSpan.FromSeconds(5)));
    ...
}
```

### Storing additional values in log table

In any place of your controller, middleware or other request processing logic do:

```csharp
var arlf = currentHttpContext.Features.Get<AzureRequestLoggerFeature>();
arlf.LogEntity.AdditionalValues.Add("SomeKey", "SomeValue");
```

### Modifying table key values

By default, `PartitionKey` contains current path, and `RowKey` contains `GetInvertedTicks()` of request timestamp.

You can change this values to any you want:

```csharp
var arlf = currentHttpContext.Features.Get<AzureRequestLoggerFeature>();
arlf.LogEntity.PartitionKey = "<some_value>";
```

Key value you provide will be sanitized before writing to table (with `LogOptions.KeySanitizationReplacement` character). You may use `LogService.SanitizeKeyValue` static method to get sanitized/actual key value earlier.