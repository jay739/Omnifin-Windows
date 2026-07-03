using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class BackupInfo
{
    [JsonPropertyName("size")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public long Date { get; set; }

    [JsonPropertyName("commit")]
    public string Commit { get; set; } = string.Empty;

    [JsonIgnore]
    public string FormattedDate => Date == 0 ? "N/A" : DateTimeOffset.FromUnixTimeSeconds(Date).LocalDateTime.ToString("g");
}

public sealed class BackupsResponse
{
    [JsonPropertyName("backups")]
    public List<BackupInfo> Backups { get; set; } = [];
}
