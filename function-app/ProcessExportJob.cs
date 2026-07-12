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

    public ProcessExportJob(ILogger<ProcessExportJob> logger, IExportRepository _repo)
    {
        _logger = logger;
        _exportRepository = _repo;
    }

    [Function(nameof(ProcessExportJob))]
    public async Task Run([QueueTrigger("export-jobs", Connection = "AzureStorage")] QueueMessage message)
    {
        _logger.LogInformation("Processing export message ID: {id}", message.MessageId);

        try
        {
            var payload = JsonSerializer.Deserialize<ExportQueueMessage>(message.MessageText);
            if (payload == null || string.IsNullOrEmpty(payload.JobId))
            {
                _logger.LogError("Malformed queue payload received.");
                return;
            }

            // 1. Fetch metadata document out of Cosmos DB
            var job = await _exportRepository.GetExportJobAsync(payload.JobId);
            if (job == null)
            {
                _logger.LogWarning("Export job document {JobId} missing from Cosmos DB.", payload.JobId);
                return;
            }

            // 2. Flip status to 'Processing'
            job.Status = "Processing";
            await _exportRepository.UpdateExportJobAsync(job); 

            _logger.LogInformation("Asynchronously processing data export for Workspace: {WorkspaceId}", job.WorkspaceId);

            // 3. Mock processing delay (This will eventually query your tasks container and write a CSV stream)
            await Task.Delay(4000); 
            string mockBlobPath = $"exports/{job.WorkspaceId}_{Guid.NewGuid()}.csv";
            
            // 4. Mark the tracking document as completed with its cloud storage destination path
            job.Status = "Completed";
            job.BlobPath = mockBlobPath;
            await _exportRepository.UpdateExportJobAsync(job);

            _logger.LogInformation("Export job {JobId} completely processed.", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception thrown inside background worker loop: {Message}", ex.Message);
            throw; 
        }
    }
}

public class ExportQueueMessage
{
    public string JobId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
}