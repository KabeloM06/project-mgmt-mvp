using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using backend.Interfaces;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly ITaskRepository _repository;
        private readonly IMemoryCache _cache;

        public TasksController(ITaskRepository repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        // 1. POST: api/tasks
        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskItem task)
        {
            if (task == null || string.IsNullOrEmpty(task.WorkspaceId))
            {
                return BadRequest("Invalid task data or missing WorkspaceId.");
            }

            // Save to the Cosmos DB emulator
            await _repository.AddTaskAsync(task);
            
            // 🔥 CACHE EVICTION: Nuke the stale cache entry for this workspace's task list
            string cacheKey = $"tasks_{task.WorkspaceId}";
            _cache.Remove(cacheKey);

            return CreatedAtAction(nameof(GetTask), new { id = task.Id, workspaceId = task.WorkspaceId }, task);
        }

        // 2. GET: api/tasks/{workspaceId}/{id}
        [HttpGet("{workspaceId}/{id}")]
        public async Task<IActionResult> GetTask(string workspaceId, string id)
        {
            // AZ-204 Best Practice: Providing both ID and PartitionKey ensures an ultra-fast, cheap 1 RU point read.
            var task = await _repository.GetTaskAsync(id, workspaceId);
            
            if (task == null)
            {
                return NotFound($"Task with ID {id} not found in workspace {workspaceId}.");
            }

            return Ok(task);
        }
    }
}