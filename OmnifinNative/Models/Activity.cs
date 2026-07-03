using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class Activity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("source_username")]
    public string SourceUsername { get; set; } = string.Empty;

    [JsonPropertyName("invite_code")]
    public string InviteCode { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonIgnore]
    public string FormattedTime => Time == 0 ? "N/A" : DateTimeOffset.FromUnixTimeSeconds(Time).LocalDateTime.ToString("g");

    [JsonIgnore]
    public string Description
    {
        get
        {
            var actor = SourceType switch
            {
                "admin" => $"Admin ({SourceUsername})",
                "daemon" => "System Daemon",
                "user" => $"User ({SourceUsername})",
                _ => string.IsNullOrEmpty(SourceUsername) ? "Anonymous" : SourceUsername
            };

            var target = string.IsNullOrEmpty(Username) ? "User" : $"'{Username}'";

            var action = Type switch
            {
                "creation" => $"created account {target}",
                "deletion" => $"deleted account {target}",
                "disabled" => $"disabled account {target}",
                "enabled" => $"enabled account {target}",
                "contactLinked" => $"linked contact method ({Value}) for {target}",
                "contactUnlinked" => $"unlinked contact method for {target}",
                "changePassword" => $"changed password for {target}",
                "resetPassword" => $"reset password for {target}",
                "createInvite" => $"generated invite code '{InviteCode}'{(string.IsNullOrEmpty(Value) ? "" : $" ({Value})")}",
                "deleteInvite" => $"deleted invite code '{InviteCode}'",
                _ => $"performed action '{Type}' on {target}"
            };

            return $"{actor} {action}";
        }
    }
}

public sealed class GetActivitiesResponse
{
    [JsonPropertyName("activities")]
    public List<Activity> Activities { get; set; } = [];

    [JsonPropertyName("last_page")]
    public bool LastPage { get; set; }
}

public sealed class SearchActivitiesRequest
{
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("sortByField")]
    public string SortByField { get; set; } = "time";

    [JsonPropertyName("ascending")]
    public bool Ascending { get; set; } = false; // Default false to get newest first

    [JsonPropertyName("searchTerms")]
    public List<string>? SearchTerms { get; set; }
}
