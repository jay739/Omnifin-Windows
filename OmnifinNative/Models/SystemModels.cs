using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class AnnouncementRequest
{
    [JsonPropertyName("users")]
    public List<string> Users { get; set; } = [];

    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("test")]
    public bool Test { get; set; }
}

public sealed class TaskInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class TasksResponse
{
    [JsonPropertyName("tasks")]
    public List<TaskInfo> Tasks { get; set; } = [];
}
