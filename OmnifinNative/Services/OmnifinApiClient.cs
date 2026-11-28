using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OmnifinNative.Models;

namespace OmnifinNative.Services;

public sealed class OmnifinApiClient : IOmnifinApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public OmnifinApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    private readonly object _sessionLock = new();
    private Uri? _server;
    private string? _access;
    private string? _refresh;
    private bool _isAdmin = true;
    private long _epoch;
    private Task? _renewal;
    public event Action<TokenResponse>? TokensRenewed;
    public event Action? SessionExpired;

    public Uri? ServerBaseAddress
    {
        get { lock (_sessionLock) { return _server; } }
        set { lock (_sessionLock) { if (_server != value) { ClearSession(); _server = value; } } }
    }

    public string? AccessToken
    {
        get { lock (_sessionLock) { return _access; } }
        set { lock (_sessionLock) { ClearSession(); _access = value; } }
    }

    public long SessionGeneration { get { lock (_sessionLock) { return _epoch; } } }

    public bool TrySetSession(TokenResponse tokens, bool isAdmin, long expectedGeneration)
    {
        lock (_sessionLock)
        {
            if (_epoch != expectedGeneration) { return false; }
            SetSession(tokens, isAdmin);
            return true;
        }
    }

    public void SetSession(TokenResponse tokens, bool isAdmin)
    {
        lock (_sessionLock)
        {
            ClearSession();
            _access = tokens.Token;
            _refresh = tokens.Refresh;
            _isAdmin = isAdmin;
        }
    }

    public void ClearSession()
    {
        lock (_sessionLock)
        {
            _epoch++;
            _access = null;
            _refresh = null;
            _renewal = null;
        }
    }

    private async Task RenewSessionAsync(string usedToken, long epoch, CancellationToken cancellationToken)
    {
        Task pending;
        lock (_sessionLock)
        {
            if (_epoch != epoch) { throw new OmnifinApiException("The session changed. Sign in again.", statusCode: System.Net.HttpStatusCode.Unauthorized); }
            if (_access != usedToken && _access is not null) { return; }
            if (_refresh is null) { ExpireSession(epoch); throw new OmnifinApiException("Your session expired. Sign in again.", statusCode: System.Net.HttpStatusCode.Unauthorized); }
            _renewal ??= RenewCoreAsync(epoch, _refresh, _isAdmin);
            pending = _renewal;
        }
        await pending.WaitAsync(cancellationToken);
    }

    private async Task RenewCoreAsync(long epoch, string refresh, bool isAdmin)
    {
        // Defer execution until the shared task is installed under the session lock.
        await Task.Yield();
        try
        {
            Task<TokenResponse> renewal;
            lock (_sessionLock)
            {
                if (_epoch != epoch) { throw new OmnifinApiException("The session changed. Sign in again.", statusCode: System.Net.HttpStatusCode.Unauthorized); }
                renewal = RefreshAsync(refresh, CancellationToken.None, isAdmin);
            }
            var tokens = await renewal;
            lock (_sessionLock)
            {
                if (_epoch != epoch) { throw new OmnifinApiException("The session changed. Sign in again.", statusCode: System.Net.HttpStatusCode.Unauthorized); }
                _access = tokens.Token;
                _refresh = tokens.Refresh ?? refresh;
                TokensRenewed?.Invoke(tokens);
            }
        }
        catch (OmnifinApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            ExpireSession(epoch);
            throw;
        }
        finally
        {
            lock (_sessionLock) { if (_epoch == epoch) { _renewal = null; } }
        }
    }

    private void ExpireSession(long epoch)
    {
        lock (_sessionLock)
        {
            if (_epoch != epoch) { return; }
            ClearSession();
            SessionExpired?.Invoke();
        }
    }

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

    public async Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken, bool isAdmin = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, isAdmin ? "/token/refresh" : "/my/token/refresh");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            return await SendAsync<TokenResponse>(request, deadline.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OmnifinApiException("Omnifin did not respond while restoring your saved session. Retry when the server is available, or sign in again.", OmnifinFailureKind.Timeout, innerException: ex);
        }
    }

    public async Task<List<RespUser>> SearchUsersAsync(UserSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Limit);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Page);
        // Each caller displays the result as a complete user list.
        var users = new List<RespUser>();
        var page = request.Page;
        while (true)
        {
            var pageRequest = new UserSearchRequest
            {
                Limit = request.Limit,
                Page = page,
                SortByField = request.SortByField,
                Ascending = request.Ascending,
                SearchTerms = request.SearchTerms,
            };
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/users")
            {
                Content = JsonContent.Create(pageRequest, options: JsonOptions),
            };

            var response = await SendAsync<GetUsersResponse>(httpRequest, cancellationToken);
            if (response.Users is not null)
            {
                users.AddRange(response.Users);
            }
            if (response.LastPage)
            {
                return users;
            }
            if (response.Users is not { Count: > 0 })
            {
                throw new OmnifinApiException("The server returned an empty user page without marking the search complete. Reload the user list to try again.");
            }
            page++;
        }
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

    public async Task<MyDetails> GetMyDetailsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/my/details");
        return await SendAsync<MyDetails>(request, cancellationToken);
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
            Content = JsonContent.Create(new { old = currentPassword, @new = newPassword }, options: JsonOptions),
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
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new OmnifinApiException(RequestFailureMessage(request, "Omnifin returned an empty response."), OmnifinFailureKind.InvalidResponse);
        }
        catch (JsonException ex)
        {
            throw new OmnifinApiException(RequestFailureMessage(request, "Omnifin returned an unreadable response. Check that the app and server versions are compatible."), OmnifinFailureKind.InvalidResponse, innerException: ex);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // /token/login and /token/refresh set their own Authorization header
        // (Basic or the refresh-token Bearer); everything else uses the
        // current access token.
        var serverBaseAddress = ServerBaseAddress;
        if (serverBaseAddress is null)
        {
            throw new OmnifinApiException("No server URL configured. Set the server URL on the login screen first.");
        }

        // Resolve each request without changing HttpClient settings after first use.
        var requestUri = request.RequestUri;
        if (requestUri is null)
        {
            request.RequestUri = serverBaseAddress;
        }
        else if (!requestUri.IsAbsoluteUri)
        {
            var baseUri = new UriBuilder(serverBaseAddress)
            {
                Path = serverBaseAddress.AbsolutePath.TrimEnd('/') + "/",
                Query = string.Empty,
                Fragment = string.Empty,
            }.Uri;
            request.RequestUri = new Uri(baseUri, requestUri.OriginalString.TrimStart('/'));
        }

        var sessionRequest = request.Headers.Authorization is null;
        string? usedToken;
        long epoch;
        lock (_sessionLock)
        {
            usedToken = _access;
            epoch = _epoch;
            if (sessionRequest && usedToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", usedToken);
            }
        }
        // Freeze request content before sending, so an approved replay is identical.
        var content = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var response = await SendTransportAsync(request, cancellationToken);
        if (sessionRequest && usedToken is not null && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var marked = response.Headers.TryGetValues("X-Omnifin-Session", out var values) && values.Contains("required");
            var safeRead = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
            if (marked || safeRead)
            {
                response.Dispose();
                await RenewSessionAsync(usedToken, epoch, cancellationToken);
                using var retry = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version, VersionPolicy = request.VersionPolicy };
                foreach (var header in request.Headers.Where(x => !x.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
                {
                    retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                if (content is not null)
                {
                    retry.Content = new ByteArrayContent(content);
                    foreach (var header in request.Content!.Headers) { retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value); }
                }
                lock (_sessionLock)
                {
                    if (_epoch != epoch) { throw new OmnifinApiException("The session changed. Sign in again.", statusCode: System.Net.HttpStatusCode.Unauthorized); }
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access);
                }
                response = await SendTransportAsync(retry, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) { ExpireSession(epoch); }
            }
        }
        return await ValidateResponseAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendTransportAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new OmnifinApiException(RequestFailureMessage(request, "Could not reach Omnifin. Check the server URL, network connection, and whether Omnifin is running."), OmnifinFailureKind.Connection, innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OmnifinApiException(RequestFailureMessage(request, "Omnifin did not respond before the request timed out. Check that the server is running and reachable."), OmnifinFailureKind.Timeout, innerException: ex);
        }

        return response;
    }

    private static async Task<HttpResponseMessage> ValidateResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                string message;
                try
                {
                    message = ApiError.Parse(body).Message;
                }
                catch (InvalidOperationException)
                {
                    message = response.ReasonPhrase ?? "The server rejected the request.";
                }
                throw new OmnifinApiException($"{(int)response.StatusCode}: {message}", statusCode: response.StatusCode);
            }
        }

        return response;
    }

    private static string RequestFailureMessage(HttpRequestMessage request, string message)
    {
        return request.Method == HttpMethod.Get || request.Method == HttpMethod.Head
            ? message + " Try again when the server is available."
            : message + " The request may have completed. Check the result before trying again.";
    }
}
