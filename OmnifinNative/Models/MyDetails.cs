using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class MyDetails
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("expiry")]
    public long Expiry { get; init; }

    [JsonPropertyName("admin")]
    public bool Admin { get; init; }

    [JsonPropertyName("accounts_admin")]
    public bool AccountsAdmin { get; init; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }

    [JsonPropertyName("email")]
    public MyDetailsContact? Email { get; init; }

    [JsonPropertyName("discord")]
    public MyDetailsContact? Discord { get; init; }

    [JsonPropertyName("telegram")]
    public MyDetailsContact? Telegram { get; init; }

    [JsonPropertyName("matrix")]
    public MyDetailsContact? Matrix { get; init; }

    [JsonPropertyName("has_referrals")]
    public bool HasReferrals { get; init; }
}

public sealed class MyDetailsContact
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
}
