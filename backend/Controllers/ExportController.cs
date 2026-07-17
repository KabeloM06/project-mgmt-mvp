using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging; // 🔥 Added for structured telemetry logging
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage.Queues; 
using backend.Infrastructure.Repositories;
using backend.Models;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IExportRepository _exportRepository;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueClient _queueClient; // 🔥 Injected our passwordless queue client directly
    private readonly ILogger<ExportController> _logger; // 🔥 Added for Application Insights tracing

    public ExportController(
        IExportRepository exportRepository, 
        BlobServiceClient blobServiceClient,
        QueueClient queueClient, // 🔥 Dependency injection handles this now
        ILogger<ExportController> logger) // 🔥 DI injects App Insights Logger automatically
    {
        _exportRepository = exportRepository;
        _blobServiceClient = blobServiceClient;
        _queueClient = queueClient;
        _logger = logger;
    }

    [HttpPost("request/{workspaceId}")]
    public async Task<IActionResult> RequestExport(string workspaceId, [FromBody] ExportRequestDto dto)
    {
        // Day 10 Telemetry: Track the incoming business request
        _logger.LogInformation("Initiating export request for Workspace {WorkspaceId}.", workspaceId);

        var job = new ExportJob
        {
            Id = Guid.NewGuid().ToString(),
            WorkspaceId = workspaceId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        // 1. Save the job entry directly into your Always-Free Cosmos DB container
        await _exportRepository.CreateExportJobAsync(job);
        _logger.LogInformation("Export job {JobId} successfully tracked in Cosmos DB with Pending status.", job.Id);
        
        // 2. Push message to the background processing queue using passwordless identity client
        try
        {
            var payload = new { JobId = job.Id, WorkspaceId = job.WorkspaceId };
            string messageText = JsonSerializer.Serialize(payload);

            // Utilizing the injected queue client directly (No Connection Strings!)
            await _queueClient.CreateIfNotExistsAsync();
            await _queueClient.SendMessageAsync(messageText);

            _logger.LogInformation("Export job {JobId} payload successfully published to Azure Storage Queue.", job.Id);
        }
        catch (Exception ex)
        {
            // Day 10 Telemetry: Log the exception with full context to Application Insights
            _logger.LogError(ex, "Cosmos DB tracking succeeded, but background queuing failed for Job {JobId}.", job.Id);
            return StatusCode(500, $"Job tracked in Cosmos DB, but background queueing failed: {ex.Message}");
        }

        return Accepted(job); 
    }

    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetExportStatus(string jobId)
    {
        _logger.LogInformation("Checking status of export job {JobId}.", jobId);

        var job = await _exportRepository.GetExportJobAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("Export job status lookup failed: Job {JobId} not found.", jobId);
            return NotFound();
        }

        return Ok(job);
    }

    [HttpGet("download/{jobId}")]
    public async Task<IActionResult> GetDownloadUrl(string jobId)
    {
        _logger.LogInformation("Generating SAS download link for completed export job {JobId}.", jobId);

        var job = await _exportRepository.GetExportJobAsync(jobId);
        if (job == null)
        {
            _logger.LogWarning("SAS URL generation failed: Job {JobId} not found.", jobId);
            return NotFound();
        }

        if (job.Status != "Completed" || string.IsNullOrEmpty(job.BlobPath))
        {
            _logger.LogWarning("SAS URL generation aborted: Job {JobId} is not in Completed state.", jobId);
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

            // 🔥 Day 11 Security Upgrade: Force the browser to download the CSV as an attachment 
            // rather than trying to open and render it in the browser window.
            sasBuilder.ContentDisposition = $"attachment; filename=\"tasks_export_{DateTime.UtcNow:yyyyMMdd}.csv\"";

            var containerClient = _blobServiceClient.GetBlobContainerClient("exports");
            var blobClient = containerClient.GetBlobClient(job.BlobPath);
            
            var sasUri = blobClient.GenerateUserDelegationSasUri(sasBuilder, userDelegationKey);

            _logger.LogInformation("Successfully generated secure User Delegation SAS URL for Job {JobId}.", jobId);
            return Ok(new { downloadUrl = sasUri.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate User Delegation SAS URL for completed Job {JobId}.", jobId);
            return StatusCode(500, $"Internal server error generating download URL: {ex.Message}");
        }
    }
}