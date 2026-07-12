using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage.Queues; // For the queue producer
using backend.Infrastructure.Repositories;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IExportRepository _exportRepository;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;

    public ExportController(
        IExportRepository exportRepository, 
        BlobServiceClient blobServiceClient,
        IConfiguration configuration)
    {
        _exportRepository = exportRepository;
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
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

        // 1. Save the job entry directly into your Always-Free Cosmos DB container
        await _exportRepository.CreateExportJobAsync(job);
        
        // 2. Push message to the background processing queue
        try
        {
            string connectionString = _configuration["AzureStorage:ConnectionString"];
            string queueName = _configuration["AzureStorage:QueueName"];
            
            QueueClient queueClient = new QueueClient(connectionString, queueName);
            await queueClient.CreateIfNotExistsAsync();

            var payload = new { JobId = job.Id, WorkspaceId = job.WorkspaceId };
            string messageText = JsonSerializer.Serialize(payload);

            await queueClient.SendMessageAsync(messageText);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Job tracked in Cosmos DB, but background queueing failed: {ex.Message}");
        }

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