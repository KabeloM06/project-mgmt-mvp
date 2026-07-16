using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Identity;                  
using Azure.Security.KeyVault.Secrets; 
using backend.Interfaces;
using backend.Infrastructure.Repositories;
using backend.Services; 

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// DAY 6 IDENTITY & CREDENTIAL BOOTSTRAPPING
// ============================================================================
// DefaultAzureCredential automatically manages local CLI tokens or production Managed Identities
var credentialOptions = new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = builder.Configuration["ManagedIdentityClientId"]
};
var azureCredential = new DefaultAzureCredential(credentialOptions);

// ============================================================================
// AZURE COSMOS DB & REPOSITORY INJECTION SETTINGS (PASSWORDLESS REFACTOR)
// ============================================================================
var cosmosSection = builder.Configuration.GetSection("CosmosDb");
string endpointUrl = cosmosSection["EndpointUrl"] ?? throw new InvalidOperationException("Cosmos EndpointUrl is missing.");
string databaseName = cosmosSection["DatabaseName"] ?? throw new InvalidOperationException("Cosmos DatabaseName is missing.");
string containerName = cosmosSection["ContainerName"] ?? throw new InvalidOperationException("Cosmos ContainerName is missing.");

// Replaced legacy 'primaryKey' authentication with token-based 'azureCredential'
var cosmosClient = new CosmosClient(endpointUrl, azureCredential, new CosmosClientOptions
{
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
});
builder.Services.AddSingleton(cosmosClient);

builder.Services.AddScoped<ITaskRepository>(sp => 
    new TaskRepository(cosmosClient, databaseName, containerName));

// ============================================================================
// DAY 6 ADDITIONAL AZURE SDK CLIENT REGISTRATIONS
// ============================================================================

// Blob Storage Client Registration
builder.Services.AddSingleton(sp =>
{
    var blobEndpoint = builder.Configuration["Storage:BlobEndpoint"]
        ?? throw new InvalidOperationException("Storage:BlobEndpoint is missing from configuration.");
        
    return new BlobServiceClient(new Uri(blobEndpoint), azureCredential);
});

// Key Vault Secret Client Registration
builder.Services.AddSingleton(sp =>
{
    var keyVaultUrl = builder.Configuration["KeyVault:Url"]
        ?? throw new InvalidOperationException("KeyVault:Url is missing from configuration.");
        
    return new SecretClient(new Uri(keyVaultUrl), azureCredential);
});

// ============================================================================
// DAY 8 AZURE STORAGE QUEUE CLIENT REGISTRATION (PASSWORDLESS)
// ============================================================================
builder.Services.AddSingleton<Azure.Storage.Queues.QueueClient>(sp =>
{
    var queueEndpoint = builder.Configuration["Storage:QueueEndpoint"]
        ?? throw new InvalidOperationException("Storage:QueueEndpoint is missing from configuration.");
    
    // Connects securely using our existing passwordless credential instance
    return new Azure.Storage.Queues.QueueClient(new Uri($"{queueEndpoint}exports"), azureCredential);
});

// register your secure config wrapper:
builder.Services.AddSingleton<JwtConfig>();

// ============================================================================
// DAY 9 PRODUCTION CORS POLICY REGISTRATION (DYNAMIC REFACTOR)
// ============================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowProductionFrontend", policy =>
    {
        // Read from App Settings (comma-separated list), fallback to defaults if null
        var configuredOrigins = builder.Configuration["AllowedOrigins"];
        var origins = !string.IsNullOrEmpty(configuredOrigins)
            ? configuredOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : new[] { "https://project-mgmt-frontend-gyvhzwhlex23g.azurewebsites.net", 
                        "http://localhost:4200" };

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================================
// DAY 3 PERFORMANCE & CACHING SERVICES
// ============================================================================
builder.Services.AddMemoryCache(); 
builder.Services.AddScoped<WorkspaceService>(); 

// ============================================================================
// DAY 8 REPOSITORY REGISTRATION
// ============================================================================
builder.Services.AddScoped<IExportRepository, ExportRepository>();

// Add services to the container.
builder.Services.AddControllers(); 
builder.Services.AddOpenApi();

// ============================================================================
// DAY 10 APPLICATION INSIGHTS TELEMETRY REGISTRATION
// ============================================================================
// Automatically tracks incoming HTTP requests, unhandled exceptions, and outgoing dependencies
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ============================================================================
// DAY 7.5 ENFORCE CORS MIDDLEWARE
// ============================================================================
// This must sit BEFORE MapControllers so preflight OPTIONS requests are handled
app.UseCors("AllowProductionFrontend");

// Maps attribute-routed API controllers automatically
app.MapControllers();

// Boilerplate Weather Endpoint
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}