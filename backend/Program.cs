using Microsoft.Azure.Cosmos;
using backend.Interfaces;
using backend.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. AZURE COSMOS DB & REPOSITORY INJECTION SETTINGS
// ============================================================================
var cosmosSection = builder.Configuration.GetSection("CosmosDb");
string endpointUrl = cosmosSection["EndpointUrl"] ?? throw new InvalidOperationException("Cosmos EndpointUrl is missing.");
string primaryKey = cosmosSection["PrimaryKey"] ?? throw new InvalidOperationException("Cosmos PrimaryKey is missing.");
string databaseName = cosmosSection["DatabaseName"] ?? throw new InvalidOperationException("Cosmos DatabaseName is missing.");
string containerName = cosmosSection["ContainerName"] ?? throw new InvalidOperationException("Cosmos ContainerName is missing.");

// Instantiate and register the CosmosClient as a Singleton to prevent socket exhaustion
var cosmosClient = new CosmosClient(endpointUrl, primaryKey, new CosmosClientOptions
{
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
});
builder.Services.AddSingleton(cosmosClient);

// Register your TaskRepository interface and implementation
builder.Services.AddScoped<ITaskRepository>(sp => 
    new TaskRepository(cosmosClient, databaseName, containerName));
// ============================================================================

// Add services to the container.
builder.Services.AddControllers(); // Essential for routing requests to Day 3 Controllers
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

// ============================================================================
// Boilerplate Weather Endpoint (Kept so you can verify local app runs safely)
// ============================================================================
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