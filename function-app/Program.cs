using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using System;
using backend.Interfaces; // 🔥 Added: Imports the ITaskRepository interface
using backend.Infrastructure.Repositories; // Imports the concrete TaskRepository / ExportRepository

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
        // DAY 6/8/11 IDENTITY & CREDENTIAL BOOTSTRAPPING (PASSWORDLESS)
        // ============================================================================
        var clientId = context.Configuration["AzureStorage:ClientId"]
            ?? context.Configuration["AzureStorage__ClientId"]
            ?? context.Configuration["ManagedIdentityClientId"];

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(clientId))
        {
            credentialOptions.ManagedIdentityClientId = clientId;
        }
        var azureCredential = new DefaultAzureCredential(credentialOptions);

        // 1. Register CosmosClient using Day 6 Passwordless standards
        services.AddSingleton((s) => {
            var endpoint = context.Configuration["CosmosDb:EndpointUrl"]
                ?? context.Configuration["CosmosDb__EndpointUrl"]
                ?? throw new InvalidOperationException("CosmosDb:EndpointUrl is missing.");
            
            return new CosmosClient(endpoint, azureCredential);
        });

        // 2. Register BlobServiceClient using Day 11 passwordless Uri endpoint
        services.AddSingleton((s) => {
            var blobUri = context.Configuration["AzureStorage:blobServiceUri"]
                ?? context.Configuration["AzureStorage__blobServiceUri"]
                ?? throw new InvalidOperationException("AzureStorage:blobServiceUri is missing.");

            return new BlobServiceClient(new Uri(blobUri), azureCredential);
        });

        // 3. Register your NoSQL Repository implementations
        services.AddScoped<IExportRepository, ExportRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>(); // Map the interface to implementation
    })
    .Build();

host.Run();