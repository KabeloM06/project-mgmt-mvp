using Microsoft.Azure.Cosmos;
using backend.Interfaces;
using backend.Infrastructure.Repositories;
using backend.Services; // 1. Added namespace for our new service layer

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// AZURE COSMOS DB & REPOSITORY INJECTION SETTINGS
// ============================================================================
var cosmosSection = builder.Configuration.GetSection("CosmosDb");
string endpointUrl = cosmosSection["EndpointUrl"] ?? throw new InvalidOperationException("Cosmos EndpointUrl is missing.");
string primaryKey = cosmosSection["PrimaryKey"] ?? throw new InvalidOperationException("Cosmos PrimaryKey is missing.");
string databaseName = cosmosSection["DatabaseName"] ?? throw new InvalidOperationException("Cosmos DatabaseName is missing.");
string containerName = cosmosSection["ContainerName"] ?? throw new InvalidOperationException("Cosmos ContainerName is missing.");

var cosmosClient = new CosmosClient(endpointUrl, primaryKey, new CosmosClientOptions
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
// DAY 3 PERFORMANCE & CACHING SERVICES
// ============================================================================
builder.Services.AddMemoryCache(); // 2. Activates standard in-memory caching
builder.Services.AddScoped<WorkspaceService>(); // 3. Registers our Cache-Aside orchestration tier

// Add services to the container.
builder.Services.AddControllers(); 
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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