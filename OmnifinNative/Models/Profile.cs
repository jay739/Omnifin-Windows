using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class ProfileInfo
{
    [JsonPropertyName("admin")]
    public bool Admin { get; set; }

    [JsonPropertyName("libraries")]
    public string Libraries { get; set; } = string.Empty;

    [JsonPropertyName("fromUser")]
    public string FromUser { get; set; } = string.Empty;

    [JsonPropertyName("ombi")]
    public bool Ombi { get; set; }

    [JsonPropertyName("jellyseerr")]
    public bool Jellyseerr { get; set; }

    [JsonPropertyName("referrals_enabled")]
    public bool ReferralsEnabled { get; set; }

    // Client-only helper to track profile name when we parse the dictionary
    [JsonIgnore]
    public string Name { get; set; } = string.Empty;

    // Client-only helper to track if this is the default profile
    [JsonIgnore]
    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;

    [JsonIgnore]
    public string AdminDisplay => Admin ? "Yes" : "No";

    [JsonIgnore]
    public string JellyseerrDisplay => Jellyseerr ? "Yes" : "No";

    [JsonIgnore]
    public string OmbiDisplay => Ombi ? "Yes" : "No";

    [JsonIgnore]
    public string ReferralsDisplay => ReferralsEnabled ? "Yes" : "No";
}

public sealed class ProfilesResponse
{
    [JsonPropertyName("profiles")]
    public Dictionary<string, ProfileInfo> Profiles { get; set; } = [];

    [JsonPropertyName("default_profile")]
    public string DefaultProfile { get; set; } = string.Empty;
}

public sealed class CreateProfileRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // Source user ID

    [JsonPropertyName("homescreen")]
    public bool Homescreen { get; set; }

    [JsonPropertyName("ombi_id")]
    public string OmbiId { get; set; } = string.Empty;

    [JsonPropertyName("jellyseerr")]
    public bool Jellyseerr { get; set; }
}
