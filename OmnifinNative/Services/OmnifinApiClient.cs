using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OmnifinNative.Models;

namespace OmnifinNative.Services;

public sealed class OmnifinApiClient : IOmnifinApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = new();

    public Uri? ServerBaseAddress
    {
        get => _httpClient.BaseAddress;
        set => _httpClient.BaseAddress = value;
    }

    // Access tokens live 20 minutes server-side; the caller is expected to
    // set this after login/refresh and re-set it after each refresh.
    public string? AccessToken { get; set; }

    public async Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/token/login");
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        return await SendAsync<TokenResponse>(request, cancellationToken);
    }

    public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/token/refresh");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

        return await SendAsync<TokenResponse>(request, cancellationToken);
    }

    public async Task<List<RespUser>> SearchUsersAsync(UserSearchRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        var response = await SendAsync<GetUsersResponse>(httpRequest, cancellationToken);
        return response.Users;
    }

    public async Task<Dictionary<string, long>> GetWatchTimeAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/watch-time");
        using var response = await SendRawAsync(request, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);

        var result = new Dictionary<string, long>();
        if (doc.RootElement.TryGetProperty("watch_time", out var watchTimeElement))
        {
            foreach (var property in watchTimeElement.EnumerateObject())
            {
                result[property.Name] = property.Value.GetInt64();
            }
        }

        return result;
    }

    public async Task EnableDisableUsersAsync(IReadOnlyList<string> userIds, bool enabled, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/enable")
        {
            Content = JsonContent.Create(new { users = userIds, enabled }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task ExtendExpiryAsync(IReadOnlyList<string> userIds, int months, int days, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/extend")
        {
            Content = JsonContent.Create(new { users = userIds, months, days }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task DeleteUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/users")
        {
            Content = JsonContent.Create(new { users = userIds }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new OmnifinApiException("Server returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // /token/login and /token/refresh set their own Authorization header
        // (Basic or the refresh-token Bearer); everything else uses the
        // current access token.
        if (_httpClient.BaseAddress is null)
        {
            throw new OmnifinApiException("No server URL configured. Set the server URL on the login screen first.");
        }

        if (request.Headers.Authorization is null && AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = ApiError.Parse(body);
            throw new OmnifinApiException($"{(int)response.StatusCode}: {error.Message}");
        }

        return response;
    }
}
