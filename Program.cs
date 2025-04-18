using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration(config =>
    {
        // Ensure we pick up local.settings.json when running locally
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWebApplication()   // Use this for ASP.NET Core Integration
    .ConfigureServices((context, services) =>
    {
        // Register BlobServiceClient for DI
        var storageConn = context.Configuration["AzureWebJobsStorage"];
        services.AddSingleton(_ => new BlobServiceClient(storageConn));
    })
    .Build();

host.Run();
