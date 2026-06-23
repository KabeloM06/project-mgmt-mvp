using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using backend.Interfaces;
using backend.Models;
using Microsoft.Azure.Cosmos;

namespace backend.Infrastructure.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly Container _container;

        // The CosmosClient is injected via Dependency Injection from Program.cs
        public TaskRepository(CosmosClient client, string databaseId, string containerId)
        {
            _container = client.GetContainer(databaseId, containerId);
        }

        // 1. Optimized Point Read (Fastest, lowest cost operation in Cosmos DB)
        public async Task<TaskItem?> GetTaskAsync(string id, string workspaceId)
        {
            try
            {
                ItemResponse<TaskItem> response = await _container.ReadItemAsync<TaskItem>(
                    id,
                    new PartitionKey(workspaceId)
                );
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        // 2. Efficient In-Partition Query (Scoped entirely to one workspace partition)
        public async Task<IEnumerable<TaskItem>> GetTasksByWorkspaceAsync(string workspaceId)
        {
            var sqlQueryText = "SELECT * FROM c WHERE c.type = 'task'";
            var queryDefinition = new QueryDefinition(sqlQueryText);
            
            var queryOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(workspaceId)
            };

            using FeedIterator<TaskItem> feedIterator = _container.GetItemQueryIterator<TaskItem>(
                queryDefinition,
                requestOptions: queryOptions
            );

            var results = new List<TaskItem>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<TaskItem> response = await feedIterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        // 3. Write Operations (Requires passing document body and corresponding PartitionKey)
        public async Task AddTaskAsync(TaskItem task)
        {
            await _container.CreateItemAsync(task, new PartitionKey(task.WorkspaceId));
        }

        public async Task AddWorkspaceAsync(Workspace workspace)
        {
            await _container.CreateItemAsync(workspace, new PartitionKey(workspace.WorkspaceId));
        }
    }
}