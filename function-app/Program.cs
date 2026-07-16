using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using System;
using backend.Infrastructure.Repositories; // Reuses your backend NoSQL repo code

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // ============================================================================
        // DAY 10 APPLICATION INSIGHTS TELEMETRY FOR ISOLATED WORKER
        // ============================================================================
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // ============================================================================
        // DAY 6/8 IDENTITY & CREDENTIAL BOOTSTRAPPING (PASSWORDLESS)
        // ============================================================================
        // Explicitly configuration-driven to avoid slow credential-probing overhead in Azure
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = context.Configuration["ManagedIdentityClientId"]
        };
        var azureCredential = new DefaultAzureCredential(credentialOptions);

        // 1. Register CosmosClient using Day 6 Passwordless standards
        services.AddSingleton((s) => {
            var endpoint = context.Configuration["CosmosDb:EndpointUrl"]
                ?? throw new InvalidOperationException("CosmosDb:EndpointUrl is missing.");
            
            return new CosmosClient(endpoint, azureCredential);
        });

        // 2. Register your NoSQL Repository implementation so the background function can use it
        services.AddScoped<IExportRepository, ExportRepository>();
    })
    .Build();

host.Run();