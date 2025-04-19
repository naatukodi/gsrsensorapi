using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Data.Tables;

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
        var storageConn = context.Configuration["SensorData"];
        services.AddSingleton(_ => new BlobServiceClient(storageConn));
        services.AddSingleton(_ => new TableServiceClient(storageConn));
    })
    .Build();

host.Run();
