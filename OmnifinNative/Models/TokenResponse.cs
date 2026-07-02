using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class TokenResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; init; }

    [JsonPropertyName("refresh")]
    public string? Refresh { get; init; }
}
