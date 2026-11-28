using System.Net;
using System.Text;
using OmnifinNative.Models;
using OmnifinNative.Services;

internal static class SessionRenewalChecks
{
    public static async Task RunAsync()
    {
        foreach (var admin in new[] { true, false })
        {
            var renewals = 0;
            var expiredReads = 0;
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var http = new HttpClient(new Handler(async (request, ct) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("token/refresh"))
                {
                    Check(request.RequestUri.AbsolutePath == (admin ? "/prefix/token/refresh" : "/prefix/my/token/refresh"), "Wrong session realm or lost prefix");
                    Interlocked.Increment(ref renewals);
                    await release.Task.WaitAsync(ct);
                    return Reply(HttpStatusCode.OK, "{\"token\":\"new\",\"refresh\":\"rotated\"}");
                }
                if (request.Headers.Authorization?.Parameter == "old")
                {
                    Interlocked.Increment(ref expiredReads);
                    return Reply(HttpStatusCode.Unauthorized, "{}");
                }
                return Reply(HttpStatusCode.OK, "{\"invites\":[]}");
            }));
            var client = new OmnifinApiClient(http) { ServerBaseAddress = new Uri("http://fixture.invalid/prefix") };
            var saved = 0;
            client.TokensRenewed += tokens => { Check(tokens.Refresh == "rotated", "Lost rotated refresh"); saved++; };
            client.SetSession(new TokenResponse { Token = "old", Refresh = "refresh" }, admin);
            var reads = Enumerable.Range(0, 6).Select(_ => client.GetInvitesAsync(CancellationToken.None)).ToArray();
            while (Volatile.Read(ref renewals) == 0) { await Task.Delay(1); }
            release.SetResult();
            await Task.WhenAll(reads);
            Check(renewals == 1 && saved == 1 && expiredReads == 6 && client.AccessToken == "new", "Concurrent reads did not share renewal");
        }
        Console.WriteLine("PASS concurrent expired reads share one renewal in the correct account realm");

        foreach (var marked in new[] { false, true })
        {
            var writes = new List<string>(); var renewals = 0;
            using var http = new HttpClient(new Handler(async (request, ct) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("token/refresh")) { renewals++; return Reply(HttpStatusCode.OK, "{\"token\":\"new\",\"refresh\":\"rotated\"}"); }
                writes.Add(await request.Content!.ReadAsStringAsync(ct));
                if (writes.Count == 1)
                {
                    var rejected = Reply(HttpStatusCode.Unauthorized, "{}");
                    if (marked) { rejected.Headers.Add("X-Omnifin-Session", "required"); }
                    return rejected;
                }
                return Reply(HttpStatusCode.OK, "{}");
            }));
            var client = new OmnifinApiClient(http) { ServerBaseAddress = new Uri("http://fixture.invalid/") };
            client.SetSession(new TokenResponse { Token = "old", Refresh = "refresh" }, true);
            try { await client.GenerateInviteAsync(new GenerateInviteRequest(), CancellationToken.None); }
            catch (OmnifinApiException) when (!marked) { }
            Check(writes.Count == (marked ? 2 : 1) && renewals == (marked ? 1 : 0), "Unmarked write replayed or marked rejection not renewed");
            if (marked) { Check(writes[0] == writes[1], "Replayed write body changed"); }
        }
        Console.WriteLine("PASS writes replay once only after an explicit before-handler rejection");

        foreach (var changeServer in new[] { false, true })
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            using var http = new HttpClient(new Handler(async (request, ct) =>
            {
                calls++;
                if (request.RequestUri!.AbsolutePath.EndsWith("token/refresh"))
                {
                    entered.SetResult(); await finish.Task.WaitAsync(ct);
                    return Reply(HttpStatusCode.OK, "{\"token\":\"late\",\"refresh\":\"late-refresh\"}");
                }
                return Reply(HttpStatusCode.Unauthorized, "{}");
            }));
            var client = new OmnifinApiClient(http) { ServerBaseAddress = new Uri("http://fixture.invalid/") };
            client.SetSession(new TokenResponse { Token = "old", Refresh = "refresh" }, true);
            var persisted = 0; client.TokensRenewed += _ => persisted++;
            var pending = client.GetInvitesAsync(CancellationToken.None);
            await entered.Task;
            if (changeServer) { client.ServerBaseAddress = new Uri("http://other.invalid/"); } else { client.ClearSession(); }
            finish.SetResult();
            try { await pending; throw new InvalidOperationException("Late renewal was accepted"); } catch (OmnifinApiException) { }
            Check(client.AccessToken is null && persisted == 0 && calls == 2, "Late refresh restored a logged-out or changed-server session");
        }
        Console.WriteLine("PASS logout and server change discard late renewal results");

        foreach (var temporary in new[] { true, false })
        {
            CredentialVault.Reset();
            using var http = new HttpClient(new Handler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("token/login")) { return Task.FromResult(Reply(HttpStatusCode.OK, "{\"token\":\"old\",\"refresh\":\"saved\"}")); }
                if (request.RequestUri.AbsolutePath.EndsWith("token/refresh") && temporary) { throw new HttpRequestException("Fixture outage"); }
                return Task.FromResult(Reply(HttpStatusCode.Unauthorized, "{}"));
            }));
            var client = new OmnifinApiClient(http) { ServerBaseAddress = new Uri("http://fixture.invalid/") };
            var auth = new AuthService(client);
            await auth.LoginAsync("fixture", "fixture", true, CancellationToken.None);
            var expiredEvents = 0; auth.SessionExpired += () => expiredEvents++;
            try { await client.GetInvitesAsync(CancellationToken.None); } catch (OmnifinApiException) { }
            Check(auth.HasSavedSession == temporary && expiredEvents == (temporary ? 0 : 1), "Outage erased saved credentials or rejection failed to route to sign-in");
            auth.Logout();
        }
        Console.WriteLine("PASS transient renewal outages preserve saved sessions and rejected renewals request sign-in");

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRenewal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var http = new HttpClient(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("token/refresh"))
            {
                started.SetResult(); await finishRenewal.Task.WaitAsync(ct);
                return Reply(HttpStatusCode.OK, "{\"token\":\"new\",\"refresh\":\"rotated\"}");
            }
            return request.Headers.Authorization?.Parameter == "new" ? Reply(HttpStatusCode.OK, "{\"invites\":[]}") : Reply(HttpStatusCode.Unauthorized, "{}");
        })))
        {
            var client = new OmnifinApiClient(http) { ServerBaseAddress = new Uri("http://fixture.invalid/") };
            client.SetSession(new TokenResponse { Token = "old", Refresh = "refresh" }, true);
            using var cancellation = new CancellationTokenSource();
            var cancelledRead = client.GetInvitesAsync(cancellation.Token);
            await started.Task;
            var survivingRead = client.GetInvitesAsync(CancellationToken.None);
            cancellation.Cancel();
            try { await cancelledRead; throw new InvalidOperationException("Cancelled waiter completed"); } catch (OperationCanceledException) { }
            finishRenewal.SetResult(); await survivingRead;
            Check(client.AccessToken == "new", "One cancelled request cancelled the shared renewal");
        }
        Console.WriteLine("PASS cancelling one request leaves shared renewal available to other callers");

        var refreshCount = 0; var requestCount = 0; var expired = 0;
        using var rejectedHttp = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("token/refresh")) { refreshCount++; return Task.FromResult(Reply(HttpStatusCode.OK, "{\"token\":\"still-rejected\"}")); }
            requestCount++; return Task.FromResult(Reply(HttpStatusCode.Unauthorized, "{}"));
        }));
        var rejectedClient = new OmnifinApiClient(rejectedHttp) { ServerBaseAddress = new Uri("http://fixture.invalid/") };
        rejectedClient.SetSession(new TokenResponse { Token = "old", Refresh = "refresh" }, true);
        rejectedClient.SessionExpired += () => expired++;
        try { await rejectedClient.GetInvitesAsync(CancellationToken.None); } catch (OmnifinApiException) { }
        Check(refreshCount == 1 && requestCount == 2 && expired == 1 && rejectedClient.AccessToken is null, "Rejected replay looped or did not return to sign-in");
        Console.WriteLine("PASS a second rejection ends the session without a refresh loop");
    }

    private static void Check(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
    private static HttpResponseMessage Reply(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
