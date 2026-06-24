using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using backend.Interfaces;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkspacesController : ControllerBase
    {
        private readonly ITaskRepository _repository;
        private readonly WorkspaceService _workspaceService;

        public WorkspacesController(ITaskRepository repository, WorkspaceService workspaceService)
        {
            _repository = repository;
            _workspaceService = workspaceService;
        }

        // 1. POST: api/workspaces
        [HttpPost]
        public async Task<IActionResult> CreateWorkspace([FromBody] Workspace workspace)
        {
            if (workspace == null || string.IsNullOrEmpty(workspace.WorkspaceId))
            {
                return BadRequest("Invalid workspace data or missing WorkspaceId.");
            }

            // Write the new workspace metadata to the Cosmos emulator
            await _repository.AddWorkspaceAsync(workspace);

            return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.WorkspaceId }, workspace);
        }

        // 2. GET: api/workspaces/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetWorkspace(string id)
        {
            // Controller queries our cached orchestration service tier directly
            var tasks = await _workspaceService.GetWorkspaceTasksWithCacheAsync(id);
            
            return Ok(new 
            { 
                WorkspaceId = id, 
                Tasks = tasks 
            });
        }
    }
}