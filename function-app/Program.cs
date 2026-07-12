using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using backend.Infrastructure.Repositories; // Reuses your backend NoSQL repo code

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // 1. Register CosmosClient using Day 6 Passwordless standards
        services.AddSingleton((s) => {
            var endpoint = context.Configuration["CosmosDb:EndpointUrl"];
            
            // Uses local environment variables in dev, Managed Identity in Azure production
            return new CosmosClient(endpoint, new DefaultAzureCredential());
        });

        // 2. Register your NoSQL Repository implementation so the background function can use it
        services.AddScoped<IExportRepository, ExportRepository>();
    })
    .Build();

host.Run();