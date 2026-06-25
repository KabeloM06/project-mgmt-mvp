using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Models;

namespace backend.Interfaces
{
    public interface ITaskRepository
    {
        Task<IEnumerable<Workspace>> GetAllWorkspacesAsync();
        Task<TaskItem> GetTaskAsync(string id, string workspaceId);
        Task<IEnumerable<TaskItem>> GetTasksByWorkspaceAsync(string workspaceId);
        Task AddTaskAsync(TaskItem task);
        Task AddWorkspaceAsync(Workspace workspace);
    }
}