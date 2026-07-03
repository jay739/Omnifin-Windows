using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class CreateUserRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("email_contact")]
    public bool EmailContact { get; set; }

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;
}

public sealed class CreateUserResponse
{
    [JsonPropertyName("user")]
    public bool User { get; set; }

    [JsonPropertyName("email")]
    public bool Email { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}
