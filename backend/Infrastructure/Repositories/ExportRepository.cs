using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using backend.Models;

namespace backend.Infrastructure.Repositories;

public class ExportRepository : IExportRepository
{
    private readonly Container _container;

    public ExportRepository(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["CosmosDb:DatabaseName"] ?? "ProjectMgmtDb";
        _container = cosmosClient.GetContainer(databaseName, "Items");
    }

    public async Task CreateExportJobAsync(ExportJob job)
    {
        await _container.CreateItemAsync(job, new PartitionKey(job.WorkspaceId));
    }

    public async Task<ExportJob?> GetExportJobAsync(string jobId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @jobId")
            .WithParameter("@jobId", jobId);

        using FeedIterator<ExportJob> iterator = _container.GetItemQueryIterator<ExportJob>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task UpdateExportJobAsync(ExportJob job)
    {
        await _container.UpsertItemAsync(job, new PartitionKey(job.WorkspaceId));
    }
}