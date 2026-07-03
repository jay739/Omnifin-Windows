using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class UsedByInfo
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }
}

public sealed class SentToInfo
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("failed")]
    public bool Failed { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

public sealed class Invite
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonPropertyName("user_expiry")]
    public bool UserExpiry { get; set; }

    [JsonPropertyName("user_months")]
    public int UserMonths { get; set; }

    [JsonPropertyName("user_days")]
    public int UserDays { get; set; }

    [JsonPropertyName("user_hours")]
    public int UserHours { get; set; }

    [JsonPropertyName("user_minutes")]
    public int UserMinutes { get; set; }

    [JsonPropertyName("valid_till")]
    public long ValidTill { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("no_limit")]
    public bool NoLimit { get; set; }

    [JsonPropertyName("remaining_uses")]
    public int RemainingUses { get; set; }

    [JsonPropertyName("user_label")]
    public string UserLabel { get; set; } = string.Empty;

    [JsonIgnore]
    public string FormattedValidTill => ValidTill == 0 ? "Never" : DateTimeOffset.FromUnixTimeMilliseconds(ValidTill).LocalDateTime.ToString("g");

    [JsonIgnore]
    public string FormattedCreated => Created == 0 ? "N/A" : DateTimeOffset.FromUnixTimeMilliseconds(Created).LocalDateTime.ToString("g");

    [JsonIgnore]
    public string UsesDisplay => NoLimit ? "Infinite" : $"{RemainingUses} remaining";
}

public sealed class GetInvitesResponse
{
    [JsonPropertyName("invites")]
    public List<Invite> Invites { get; set; } = [];
}

public sealed class GenerateInviteRequest
{
    [JsonPropertyName("months")]
    public int Months { get; set; }

    [JsonPropertyName("days")]
    public int Days { get; set; }

    [JsonPropertyName("hours")]
    public int Hours { get; set; }

    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }

    [JsonPropertyName("user-expiry")]
    public bool UserExpiry { get; set; }

    [JsonPropertyName("user-months")]
    public int UserMonths { get; set; }

    [JsonPropertyName("user-days")]
    public int UserDays { get; set; }

    [JsonPropertyName("user-hours")]
    public int UserHours { get; set; }

    [JsonPropertyName("user-minutes")]
    public int UserMinutes { get; set; }

    [JsonPropertyName("send-to")]
    public string SendTo { get; set; } = string.Empty;

    [JsonPropertyName("multiple-uses")]
    public bool MultipleUses { get; set; }

    [JsonPropertyName("no-limit")]
    public bool NoLimit { get; set; }

    [JsonPropertyName("remaining-uses")]
    public int RemainingUses { get; set; }

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("user_label")]
    public string UserLabel { get; set; } = string.Empty;
}
