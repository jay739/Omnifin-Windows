using System.Text.Json.Serialization;

namespace OmnifinNative.Models;

public sealed class UserSearchRequest
{
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 50;

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("sortByField")]
    public string SortByField { get; init; } = "name";

    [JsonPropertyName("ascending")]
    public bool Ascending { get; init; } = true;

    [JsonPropertyName("searchTerms")]
    public List<string>? SearchTerms { get; init; }
}
