using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using backend.Interfaces;
using backend.Models;

namespace backend.Services
{
    public class WorkspaceService
    {
        private readonly ITaskRepository _repository;
        private readonly IMemoryCache _cache;

        public WorkspaceService(ITaskRepository repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<IEnumerable<TaskItem>> GetWorkspaceTasksWithCacheAsync(string workspaceId)
        {
            string cacheKey = $"tasks_{workspaceId}";

            // Check if the board data exists in our local memory cache
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<TaskItem>? tasks))
            {
                // Cache Miss: Go down to the Cosmos DB container
                tasks = await _repository.GetTasksByWorkspaceAsync(workspaceId);

                // Save it to the cache with a 5-minute expiration sliding window
                _cache.Set(cacheKey, tasks, TimeSpan.FromMinutes(5));
            }

            return tasks ?? new List<TaskItem>();
        }
    }
}