using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class UserSettingsRequest
{
    [JsonPropertyName("from")]
    public string From { get; set; } = "profile"; // "user" or "profile"

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonPropertyName("apply_to")]
    public List<string> ApplyTo { get; set; } = [];

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // Source user ID (if from == "user")

    [JsonPropertyName("configuration")]
    public bool Configuration { get; set; } // Matches the confusing Go name 'configuration' (policy)

    [JsonPropertyName("homescreen")]
    public bool Homescreen { get; set; }

    [JsonPropertyName("ombi")]
    public bool Ombi { get; set; }

    [JsonPropertyName("jellyseerr")]
    public bool Jellyseerr { get; set; }
}
