using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Azure.Storage.Queues.Models;
using Azure.Storage.Blobs; // For streaming directly to Blob Storage
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using backend.Interfaces; // 🔥 Added: Resolves ITaskRepository
using backend.Infrastructure.Repositories; // Reuses your existing export repos
using backend.Models; // Contains your TaskItem model

namespace Company.Function;

public class ProcessExportJob
{
    private readonly ILogger<ProcessExportJob> _logger;
    private readonly IExportRepository _exportRepository;
    private readonly ITaskRepository _taskRepository; // Injected for querying real task data
    private readonly BlobServiceClient _blobServiceClient; // Injected for writing CSV files

    public ProcessExportJob(
        ILogger<ProcessExportJob> logger, 
        IExportRepository exportRepository,
        ITaskRepository taskRepository,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _exportRepository = exportRepository;
        _taskRepository = taskRepository;
        _blobServiceClient = blobServiceClient;
    }

    [Function(nameof(ProcessExportJob))]
    public async Task Run([QueueTrigger("exports", Connection = "AzureStorage")] QueueMessage message)
    {
        // Day 10 Telemetry: Track execution start with structured variables
        _logger.LogInformation("Background execution triggered. Processing queue message ID: {MessageId}", message.MessageId);

        ExportQueueMessage? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ExportQueueMessage>(message.MessageText);
            if (payload == null || string.IsNullOrEmpty(payload.JobId))
            {
                _logger.LogError("Aborting process: Malformed queue payload received with message ID {MessageId}.", message.MessageId);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aborting process: Failed to deserialize queue payload. Raw content: {Content}", message.MessageText);
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

        try
        {
            _logger.LogInformation("Retrieving live task dataset for Workspace {WorkspaceId}...", payload.WorkspaceId);

            // 3. Query Cosmos DB for real workspace task records
            var tasks = await _taskRepository.GetTasksByWorkspaceAsync(payload.WorkspaceId);
            var tasksList = tasks?.ToList() ?? new List<TaskItem>(); // 🔥 Corrected: Replaced ProjectTask with TaskItem

            _logger.LogInformation("Found {TaskCount} tasks. Commencing CSV compilation...", tasksList.Count);

            // 4. Generate CSV payload in-memory with Excel Injection protection
            var csv = new StringBuilder();
            csv.AppendLine("TaskId,Title,Status,AssignedTo,Tags");

            foreach (var task in tasksList)
            {
                var formattedTags = string.Join("|", task.Tags ?? new List<string>());
                
                csv.AppendLine($"{task.Id},{EscapeCsv(task.Title)},{task.Status},{EscapeCsv(task.AssignedTo)},{EscapeCsv(formattedTags)}");
            }

            // 5. Connect to 'exports' container and upload our memory stream
            var containerClient = _blobServiceClient.GetBlobContainerClient("exports");
            await containerClient.CreateIfNotExistsAsync();

            // Establish a clear, structured folder hierarchy: workspaceId/jobId.csv
            string targetBlobName = $"{payload.WorkspaceId}/{job.Id}.csv";
            var blobClient = containerClient.GetBlobClient(targetBlobName);

            _logger.LogInformation("Streaming compiled CSV directly to Azure Blob Storage: '{BlobName}'...", targetBlobName);

            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
            await blobClient.UploadAsync(memoryStream, overwrite: true);

            // 6. Mark the tracking document as completed with its cloud storage destination path
            job.Status = "Completed";
            job.BlobPath = targetBlobName;
            job.CompletedAt = DateTime.UtcNow;
            await _exportRepository.UpdateExportJobAsync(job);

            // Day 10/11 Telemetry: Record successful execution end-of-lifecycle
            _logger.LogInformation("Job {JobId} completed successfully. Export generated at path: {BlobPath}", job.Id, job.BlobPath);
        }
        catch (Exception ex)
        {
            // Day 10 Telemetry: Bubble the exception stack trace straight into App Insights dashboards
            _logger.LogError(ex, "Uncaught exception thrown inside background worker loop for message ID {MessageId}.", message.MessageId);
            
            // Gracefully flag the job metadata as Failed so the frontend doesn't hang in a perpetual loading spinner
            job.Status = "Failed";
            job.ErrorMessage = ex.Message;
            await _exportRepository.UpdateExportJobAsync(job);
            
            throw; 
        }
    }

    /// <summary>
    /// Escapes CSV field parameters to prevent CSV Excel Injection vulnerabilities.
    /// Prefixes formula characters (=, +, -, @) with a single quote to neutralize them.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        // Standard double-quote escaping for values containing commas or quotes
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            value = $"\"{value.Replace("\"", "\"\"")}\"";
        }

        // Formula neutralization prefixing
        if (value.StartsWith("=") || value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("@"))
        {
            value = $"'{value}";
        }

        return value;
    }
}

public class ExportQueueMessage
{
    public string JobId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
}