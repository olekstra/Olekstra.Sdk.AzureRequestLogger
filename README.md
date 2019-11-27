# Olekstra.Sdk.AzureRequestLogger

Middleware that allows you to log (to Azure Storage Table) all requests (and your responses) for some path. Useful for logging your API calls.

Based on [iflight/RequestLogger.AzureTable](https://github.com/iflight/RequestLogger.AzureTable).

[![NuGet](https://img.shields.io/nuget/v/Olekstra.Sdk.AzureRequestLogger.svg?maxAge=86400&style=flat)](https://www.nuget.org/packages/Olekstra.Sdk.AzureRequestLogger/)

## Main features

* Log records are saved in separate thread (your main pipeline performance not affected) and batched;
* Request/response body text is captured;
* Very long request/response body is truncated;
* Log tables can have suffix (with year and querter or month), so you can easy erase old logs by dropping tables;

## How to use

In `Startup.cs`:

```csharp
public async void Configure(IApplicationBuilder app,ILoggerFactory loggerfactory)
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
