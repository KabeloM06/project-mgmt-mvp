using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace backend.Backend.Models
{
    public class TaskItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("workspaceId")]
        public string WorkspaceId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "task";

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } // e.g., "To Do", "In Progress", "Done"

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("assignedTo")]
        public string AssignedTo { get; set; }

        [JsonPropertyName("subTasks")]
        public List<SubTask> SubTasks { get; set; } = new();
    }

    public class SubTask
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; } = false;
    }
}