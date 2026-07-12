using System.Threading.Tasks;
using backend.Models;

namespace backend.Infrastructure.Repositories;

public interface IExportRepository
{
    Task CreateExportJobAsync(ExportJob job);
    Task<ExportJob?> GetExportJobAsync(string jobId); // Must return Task<ExportJob?>
    Task UpdateExportJobAsync(ExportJob job);
}