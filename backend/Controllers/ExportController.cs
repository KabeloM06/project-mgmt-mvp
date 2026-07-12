using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using backend.Infrastructure.Repositories;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IExportRepository _exportRepository;
    private readonly BlobServiceClient _blobServiceClient;

    public ExportController(IExportRepository exportRepository, BlobServiceClient blobServiceClient)
    {
        _exportRepository = exportRepository;
        _blobServiceClient = blobServiceClient;
    }

    [HttpPost("request/{workspaceId}")]
    public async Task<IActionResult> RequestExport(string workspaceId, [FromBody] ExportRequestDto dto)
    {
        var job = new ExportJob
        {
            Id = Guid.NewGuid().ToString(),
            WorkspaceId = workspaceId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _exportRepository.CreateExportJobAsync(job);
        
        return Accepted(job); 
    }

    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetExportStatus(string jobId)
    {
        var job = await _exportRepository.GetExportJobAsync(jobId);
        if (job == null)
        {
            return NotFound();
        }

        return Ok(job);
    }

    [HttpGet("download/{jobId}")]
    public async Task<IActionResult> GetDownloadUrl(string jobId)
    {
        var job = await _exportRepository.GetExportJobAsync(jobId);
        if (job == null)
        {
            return NotFound();
        }

        if (job.Status != "Completed" || string.IsNullOrEmpty(job.BlobPath))
        {
            return BadRequest("Export job is not completed or file path is missing.");
        }

        try
        {
            var options = new BlobGetUserDelegationKeyOptions(DateTimeOffset.UtcNow.AddDays(1))
            {
                StartsOn = DateTimeOffset.UtcNow
            };
            
            var userDelegationKeyResponse = await _blobServiceClient.GetUserDelegationKeyAsync(options);
            var userDelegationKey = userDelegationKeyResponse.Value;

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "exports",
                BlobName = job.BlobPath,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var containerClient = _blobServiceClient.GetBlobContainerClient("exports");
            var blobClient = containerClient.GetBlobClient(job.BlobPath);
            
            var sasUri = blobClient.GenerateUserDelegationSasUri(sasBuilder, userDelegationKey);

            return Ok(new { downloadUrl = sasUri.ToString() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error generating download URL: {ex.Message}");
        }
    }
}