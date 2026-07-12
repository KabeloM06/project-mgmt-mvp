namespace backend.Models;

public class ExportJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkspaceId { get; set; } = null!;
    public string RequestedBy { get; set; } = null!;
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? BlobPath { get; set; }
    public string? ErrorMessage { get; set; }
}

public record ExportRequestDto(string RequestedBy);
public record ExportPayload(string JobId, string WorkspaceId);