using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using backend.Infrastructure.Repositories;
using backend.Models;

namespace Company.Function;

public class ProcessExportJob
{
    private readonly ILogger<ProcessExportJob> _logger;
    private readonly IExportRepository _exportRepository;

    public ProcessExportJob(ILogger<ProcessExportJob> logger, IExportRepository repo)
    {
        _logger = logger;
        _exportRepository = repo;
    }

    [Function(nameof(ProcessExportJob))]
    // 🔥 Aligned Queue trigger name to "exports" to match main.bicep and Web API configurations
    public async Task Run([QueueTrigger("exports", Connection = "AzureStorage")] QueueMessage message)
    {
        // Day 10 Telemetry: Track execution start with structured variables
        _logger.LogInformation("Background execution triggered. Processing queue message ID: {MessageId}", message.MessageId);

        try
        {
            var payload = JsonSerializer.Deserialize<ExportQueueMessage>(message.MessageText);
            if (payload == null || string.IsNullOrEmpty(payload.JobId))
            {
                _logger.LogError("Aborting process: Malformed queue payload received with message ID {MessageId}.", message.MessageId);
                return;
            }

            // 1. Fetch metadata document out of Cosmos DB
            var job = await _exportRepository.GetExportJobAsync(payload.JobId);
            if (job == null)
            {
                _logger.LogWarning("Execution mismatch: Export job document {JobId} is missing from Cosmos DB.", payload.JobId);
                return;
            }

            // 2. Flip status to 'Processing'
            job.Status = "Processing";
            await _exportRepository.UpdateExportJobAsync(job); 

            _logger.LogInformation("Job {JobId} status updated to 'Processing' for Workspace {WorkspaceId}.", job.Id, job.WorkspaceId);

            // 3. Mock processing delay (This will eventually query your tasks container and write a CSV stream)
            await Task.Delay(4000); 
            string mockBlobPath = $"exports/{job.WorkspaceId}_{Guid.NewGuid()}.csv";
            
            // 4. Mark the tracking document as completed with its cloud storage destination path
            job.Status = "Completed";
            job.BlobPath = mockBlobPath;
            await _exportRepository.UpdateExportJobAsync(job);

            // Day 10 Telemetry: Record successful execution end-of-lifecycle
            _logger.LogInformation("Job {JobId} completed successfully. Export generated at path: {BlobPath}", job.Id, job.BlobPath);
        }
        catch (Exception ex)
        {
            // Day 10 Telemetry: Bubble the exception stack trace straight into App Insights dashboards
            _logger.LogError(ex, "Uncaught exception thrown inside background worker loop for message ID {MessageId}.", message.MessageId);
            throw; 
        }
    }
}

public class ExportQueueMessage
{
    public string JobId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
}