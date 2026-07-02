using System.Text.Json;

namespace OmnifinNative.Models;

// omnifin's error responses aren't consistently shaped across handlers
// (apiErrorBody, stringResponse, or boolResponse) - this pulls whatever
// message it can find rather than binding to one fixed schema.
public sealed class ApiError
{
    public required string Message { get; init; }
    public string? Code { get; init; }
    public string? Hint { get; init; }

    public static ApiError Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var message = root.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString() ?? "Unknown error"
                : "Unknown error";

            var code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
            var hint = root.TryGetProperty("hint", out var hintProp) ? hintProp.GetString() : null;

            return new ApiError { Message = message, Code = code, Hint = hint };
        }
        catch (JsonException)
        {
            return new ApiError { Message = string.IsNullOrWhiteSpace(body) ? "Unknown error" : body };
        }
    }
}
