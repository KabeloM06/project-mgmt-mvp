using System;
using System.Text.Json.Serialization;

namespace backend.Models
{
    public class Workspace
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("workspaceId")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "workspace";

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("ownerId")]
        public string OwnerId { get; set; }
    }
}