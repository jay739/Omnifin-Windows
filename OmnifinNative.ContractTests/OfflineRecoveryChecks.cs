using System.Net;
using System.Net.Sockets;
using System.Text;
using OmnifinNative.Models;
using OmnifinNative.Services;

internal static class OfflineRecoveryChecks
{
    private static readonly Uri FixtureAddress = new("http://fixture.invalid/");
    private const string RefreshTarget = "OmnifinNative:refresh-token";
    private const string RoleTarget = "OmnifinNative:is-admin";

    public static async Task RunAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var disconnected = new OmnifinApiClient { ServerBaseAddress = new Uri($"http://127.0.0.1:{port}/") };
        var offlineError = await ExpectFailure(() => disconnected.CreateBackupAsync(CancellationToken.None), OmnifinFailureKind.Connection);
        Check(offlineError.Message.Contains("may have completed"), "An offline write did not explain its uncertain outcome.");
        Console.WriteLine("PASS an unreachable server returns a handled connection error");

        var requests = 0;
        using var transport = new HttpClient(new FixtureHandler((_, _) =>
        {
            requests++;
            throw new HttpRequestException("Fixture disconnected after a write.");
        }));
        var client = new OmnifinApiClient(transport) { ServerBaseAddress = FixtureAddress };
        var failure = await ExpectFailure(() => client.CreateBackupAsync(CancellationToken.None), OmnifinFailureKind.Connection);
        Check(requests == 1 && failure.InnerException is HttpRequestException, "A failed write was replayed or lost its cause.");
        Console.WriteLine("PASS failed writes are not retried automatically");

        var timeoutRequests = 0;
        using var slowTransport = new HttpClient(new FixtureHandler(async (_, cancellationToken) =>
        {
            timeoutRequests++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Reply(HttpStatusCode.OK, "{}");
        }))
        { Timeout = TimeSpan.FromMilliseconds(100) };
        var slow = new OmnifinApiClient(slowTransport) { ServerBaseAddress = FixtureAddress };
        var timeout = await ExpectFailure(() => slow.CreateBackupAsync(CancellationToken.None), OmnifinFailureKind.Timeout);
        Check(timeoutRequests == 1 && timeout.Message.Contains("Check the result"), "A timed-out write was replayed or given unsafe retry guidance.");
        Console.WriteLine("PASS timeouts return a handled error without replaying writes");

        using var restoreTransport = new HttpClient(new FixtureHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Reply(HttpStatusCode.OK, "{}");
        }))
        { Timeout = Timeout.InfiniteTimeSpan };
        using var restoreLimit = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var stalledRestore = new OmnifinApiClient(restoreTransport) { ServerBaseAddress = FixtureAddress };
        await ExpectFailure(() => stalledRestore.RefreshAsync("fixture", restoreLimit.Token), OmnifinFailureKind.Timeout);
        Console.WriteLine("PASS saved-session restoration has a bounded wait");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            await client.CreateBackupAsync(cancelled.Token);
            throw new InvalidOperationException("Caller cancellation was ignored.");
        }
        catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
        {
        }
        Console.WriteLine("PASS caller cancellation is distinct from a server timeout");

        foreach (var body in new[] { "not JSON", "null", "{}" })
        {
            using var invalidTransport = new HttpClient(new FixtureHandler((_, _) => Task.FromResult(Reply(HttpStatusCode.OK, body))));
            var invalid = new OmnifinApiClient(invalidTransport) { ServerBaseAddress = FixtureAddress };
            await ExpectFailure(() => invalid.RefreshAsync("fixture", CancellationToken.None), OmnifinFailureKind.InvalidResponse);
        }
        Console.WriteLine("PASS malformed and missing response data return handled errors");

        using var invalidWriteTransport = new HttpClient(new FixtureHandler((_, _) => Task.FromResult(Reply(HttpStatusCode.OK, "not JSON"))));
        var invalidWrite = new OmnifinApiClient(invalidWriteTransport) { ServerBaseAddress = FixtureAddress };
        var invalidWriteError = await ExpectFailure(() => invalidWrite.CreateUserAsync(new CreateUserRequest
        {
            Username = "fixture",
            Password = "fixture",
            Email = "",
            EmailContact = false,
            Profile = "none",
        }, CancellationToken.None), OmnifinFailureKind.InvalidResponse);
        Check(invalidWriteError.Message.Contains("Check the result"), "An unreadable write response encouraged repeating an uncertain operation.");
        Console.WriteLine("PASS unreadable write responses explain the uncertain outcome");

        CredentialVault.Reset();
        SaveSession();
        var online = false;
        using var recoveryTransport = new HttpClient(new FixtureHandler((request, _) =>
        {
            Check(request.RequestUri!.AbsolutePath == "/token/refresh", "Session recovery changed its route.");
            if (!online)
            {
                throw new HttpRequestException("Fixture outage.");
            }
            return Task.FromResult(Reply(HttpStatusCode.OK, "{\"token\":\"recovered\",\"refresh\":\"rotated\"}"));
        }));
        var recoveryClient = new OmnifinApiClient(recoveryTransport) { ServerBaseAddress = FixtureAddress };
        var auth = new AuthService(recoveryClient);
        Check(!await auth.TryRestoreSessionAsync(CancellationToken.None), "An offline session was restored.");
        Check(auth.HasSavedSession && auth.IsAdmin && !auth.IsAuthenticated && auth.RestoreError is not null,
            "Temporary connection failure lost the saved session, role or recovery message.");
        online = true;
        Check(await auth.TryRestoreSessionAsync(CancellationToken.None), "Recovery failed after the server returned.");
        Check(auth.IsAuthenticated && auth.IsAdmin && auth.RestoreError is null && CredentialVault.Read(RefreshTarget) == "rotated",
            "Recovered session state was not applied.");
        Console.WriteLine("PASS offline startup preserves the saved session and succeeds on retry");

        foreach (var (status, body) in new[]
        {
            (HttpStatusCode.BadGateway, "{\"error\":\"Temporary service outage\"}"),
            (HttpStatusCode.ServiceUnavailable, "{\"error\":\"Temporary service outage\"}"),
            (HttpStatusCode.OK, "not JSON"),
        })
        {
            CredentialVault.Reset();
            SaveSession();
            using var unavailable = new HttpClient(new FixtureHandler((_, _) => Task.FromResult(Reply(status, body))));
            var service = new AuthService(new OmnifinApiClient(unavailable) { ServerBaseAddress = FixtureAddress });
            Check(!await service.TryRestoreSessionAsync(CancellationToken.None), "An unavailable session was restored.");
            Check(service.HasSavedSession && service.IsAdmin && service.RestoreError is not null,
                "A transient server or response failure erased the saved session.");
        }
        Console.WriteLine("PASS temporary server and response errors preserve saved sessions");

        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            CredentialVault.Reset();
            SaveSession();
            using var rejected = new HttpClient(new FixtureHandler((_, _) => Task.FromResult(Reply(status, "[]"))));
            var service = new AuthService(new OmnifinApiClient(rejected) { ServerBaseAddress = FixtureAddress });
            Check(!await service.TryRestoreSessionAsync(CancellationToken.None), "A rejected session was restored.");
            Check(!service.HasSavedSession && !service.IsAdmin && service.RestoreError!.Contains("Sign in again"),
                "A rejected saved session did not request a fresh login.");
        }
        CredentialVault.Reset();
        Console.WriteLine("PASS rejected saved sessions request a fresh login");
    }

    private static void SaveSession()
    {
        CredentialVault.Save(RefreshTarget, "fixture", "fixture-refresh");
        CredentialVault.Save(RoleTarget, "is-admin", "true");
    }

    private static async Task<OmnifinApiException> ExpectFailure(Func<Task> action, OmnifinFailureKind kind)
    {
        try
        {
            await action();
        }
        catch (OmnifinApiException ex)
        {
            Check(ex.Kind == kind, $"Expected {kind}, received {ex.Kind}.");
            return ex;
        }
        throw new InvalidOperationException($"Expected a handled {kind} failure.");
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}

namespace OmnifinNative.Services
{
    // Session-recovery checks use an isolated store and never access Windows credentials.
    internal static class CredentialVault
    {
        private static readonly Dictionary<string, string> Values = [];

        public static string? Read(string target) => Values.GetValueOrDefault(target);
        public static void Save(string target, string userName, string secret) => Values[target] = secret;
        public static void Delete(string target) => Values.Remove(target);
        public static void Reset() => Values.Clear();
    }
}
