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

    public async Task<TokenResponse> LoginUserAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/my/token/login");
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

    public async Task<List<Invite>> GetInvitesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/invites");
        var response = await SendAsync<GetInvitesResponse>(request, cancellationToken);
        return response.Invites;
    }

    public async Task GenerateInviteAsync(GenerateInviteRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/invites")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        await SendRawAsync(httpRequest, cancellationToken);
    }

    public async Task DeleteInviteAsync(string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/invites")
        {
            Content = JsonContent.Create(new { code }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task<ProfilesResponse> GetProfilesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/profiles");
        return await SendAsync<ProfilesResponse>(request, cancellationToken);
    }

    public async Task CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/profiles")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        await SendRawAsync(httpRequest, cancellationToken);
    }

    public async Task DeleteProfileAsync(string name, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/profiles")
        {
            Content = JsonContent.Create(new { name }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task SetDefaultProfileAsync(string name, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/profiles/default")
        {
            Content = JsonContent.Create(new { name }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task<GetActivitiesResponse> GetActivitiesAsync(SearchActivitiesRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/activity")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        return await SendAsync<GetActivitiesResponse>(httpRequest, cancellationToken);
    }

    public async Task AnnounceAsync(AnnouncementRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/users/announce")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        await SendRawAsync(httpRequest, cancellationToken);
    }

    public async Task<string> GetLogsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/logs");
        using var response = await SendRawAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("log", out var logElement))
        {
            return logElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    public async Task<List<TaskInfo>> GetTasksAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/tasks");
        var response = await SendAsync<TasksResponse>(request, cancellationToken);
        return response.Tasks;
    }

    public async Task RunTaskAsync(string taskUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, taskUrl);
        await SendRawAsync(request, cancellationToken);
    }

    public async Task RestartServerAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/restart");
        await SendRawAsync(request, cancellationToken);
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/user")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        return await SendAsync<CreateUserResponse>(httpRequest, cancellationToken);
    }

    public async Task ModifyEmailsAsync(Dictionary<string, string> userEmails, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/emails")
        {
            Content = JsonContent.Create(userEmails, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task ModifyLabelsAsync(Dictionary<string, string> userLabels, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/labels")
        {
            Content = JsonContent.Create(userLabels, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task SetAccountsAdminAsync(Dictionary<string, bool> admins, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/accounts-admin")
        {
            Content = JsonContent.Create(admins, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task ApplySettingsAsync(UserSettingsRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/users/settings")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };

        await SendRawAsync(httpRequest, cancellationToken);
    }

    public async Task<List<BackupInfo>> GetBackupsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/backups");
        var response = await SendAsync<BackupsResponse>(request, cancellationToken);
        return response.Backups;
    }

    public async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/backups");
        await SendRawAsync(request, cancellationToken);
    }

    public async Task RestoreBackupAsync(string filename, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/backups/restore/{filename}");
        await SendRawAsync(request, cancellationToken);
    }

    public async Task<Models.GetServerConfigResponse> GetConfigAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/config");
        return await SendAsync<Models.GetServerConfigResponse>(request, cancellationToken);
    }

    public async Task SaveConfigAsync(Dictionary<string, Dictionary<string, string>> config, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/config");
        request.Content = JsonContent.Create(config, options: JsonOptions);
        using var response = await SendRawAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new OmnifinApiException($"HTTP {(int)response.StatusCode}: {body}");
        }
    }

    public async Task<RespUser> GetMyDetailsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/my/details");
        return await SendAsync<RespUser>(request, cancellationToken);
    }

    public async Task UpdateMyEmailAsync(string email, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/my/email")
        {
            Content = JsonContent.Create(new { email }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/my/password")
        {
            Content = JsonContent.Create(new { password = currentPassword, newPassword }, options: JsonOptions),
        };

        await SendRawAsync(request, cancellationToken);
    }

    public async Task UpdateMyContactMethodsAsync(bool email, bool telegram, bool discord, bool matrix, CancellationToken cancellationToken)
    {
        var methods = new Dictionary<string, object>
        {
            ["email"] = email,
            ["telegram"] = telegram,
            ["discord"] = discord,
            ["matrix"] = matrix
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/my/contact")
        {
            Content = JsonContent.Create(methods, options: JsonOptions),
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
