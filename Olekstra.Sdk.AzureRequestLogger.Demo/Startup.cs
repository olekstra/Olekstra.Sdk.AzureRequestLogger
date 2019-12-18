namespace Olekstra.Sdk.AzureRequestLogger.Demo
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Nothing
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseAzureRequestLogger(o =>
                o.For("/test")
                 .For("/test2")
                 .UsingConnectionString("UseDevelopmentStorage=true;")
                 .IntoTable("logs", TableNameSuffixMode.YearAndQuarter)
                 .Every(TimeSpan.FromSeconds(5)));

            app.UseAzureRequestLogger(o => o.For("/test3").IntoTable("test3", TableNameSuffixMode.YearAndQuarter).Every(TimeSpan.FromSeconds(5)));

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapGet("/test2", async context =>
                {
                    var f = context.Features.Get<AzureRequestLoggerFeature>();
                    f.LogEntity.PartitionKey = "!!!" + f.LogEntity.PartitionKey;

                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapPost("/test2", async context =>
                {
                    using var reader = new System.IO.StreamReader(context.Request.Body);
                    var text = await reader.ReadToEndAsync();
                    await context.Response.WriteAsync("Hello, " + text);
                });
            });
        }
    }
}
