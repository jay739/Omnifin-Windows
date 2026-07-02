using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

// Plain get/set (not required/init): this type is bound via x:DataType in
// AccountListPage's DataTemplate, and WinUI's compiled-binding type-info
// generator constructs objects via a parameterless constructor + property
// assignment, which required/init members are incompatible with (see the
// same gotcha hit on HomelabTray's ServiceStatus).
public sealed class RespUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("last_active")]
    public long LastActive { get; set; }

    [JsonPropertyName("admin")]
    public bool Admin { get; set; }

    [JsonPropertyName("expiry")]
    public long Expiry { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    // Populated client-side after joining against GET /users/watch-time,
    // which keys its response by username rather than user ID.
    [JsonIgnore]
    public long WatchTimeSeconds { get; set; }
}

public sealed class GetUsersResponse
{
    [JsonPropertyName("users")]
    public required List<RespUser> Users { get; init; }

    [JsonPropertyName("last_page")]
    public bool LastPage { get; init; }
}
