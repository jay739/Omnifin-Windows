using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using OmnifinNative.Models;
using OmnifinNative.Services;

await OfflineRecoveryChecks.RunAsync();
await SessionRenewalChecks.RunAsync();

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
await using var first = new LocalApiFixture();
await using var second = new LocalApiFixture();
var client = new OmnifinApiClient { ServerBaseAddress = first.BaseAddress };

try
{
    await client.LoginAsync("fixture", "wrong", timeout.Token);
    throw new InvalidOperationException("The fixture accepted the intentionally wrong password.");
}
catch (OmnifinApiException)
{
}

client.ServerBaseAddress = first.BaseAddress;
var response = await client.LoginAsync("fixture", "correct", timeout.Token);
Check(response.Token == "fixture-token", "Corrected credentials did not succeed.");
Console.WriteLine("PASS corrected login after a failed attempt");

client.ServerBaseAddress = first.BaseAddress;
await client.LoginAsync("fixture", "correct", timeout.Token);
Check(first.RequestCount == 3, "Repeated login did not reach the original server.");
Console.WriteLine("PASS repeated sign-in with the same server");

client.ServerBaseAddress = second.BaseAddress;
await client.LoginAsync("fixture", "correct", timeout.Token);
Check(first.RequestCount == 3 && second.RequestCount == 1, "A server change used the wrong destination.");
Console.WriteLine("PASS server change after earlier requests");

Check(second.LastRequestTarget == "/token/login", "Root server URL did not retain the login route.");
foreach (var (prefix, expectedPath) in new[]
{
    ("omnifin", "/omnifin/token/login"),
    ("omnifin/", "/omnifin/token/login"),
    ("nested/omnifin/", "/nested/omnifin/token/login"),
    ("my%20media/", "/my%20media/token/login"),
    ("omnifin?ignored=true#settings", "/omnifin/token/login"),
})
{
    client.ServerBaseAddress = new Uri(first.BaseAddress, prefix);
    await client.LoginAsync("fixture", "correct", timeout.Token);
    Check(first.LastRequestTarget == expectedPath, $"Server prefix was lost: expected {expectedPath}, got {first.LastRequestTarget}.");
}
Console.WriteLine("PASS server URL prefixes with and without a trailing slash");

client.ServerBaseAddress = new Uri(first.BaseAddress, "nested/omnifin/");
await client.LoginUserAsync("fixture", "correct", timeout.Token);
Check(first.LastRequestTarget == "/nested/omnifin/my/token/login", "Self-service login lost the server prefix.");
Console.WriteLine("PASS self-service login under a server URL prefix");

client.AccessToken = "fixture-access";
await client.RunTaskAsync(new Uri(second.BaseAddress, "absolute-task").AbsoluteUri, timeout.Token);
Check(second.LastRequestTarget == "/absolute-task", "An absolute request URL was rewritten.");
Console.WriteLine("PASS absolute request URLs are preserved");

client.ServerBaseAddress = first.BaseAddress;
client.AccessToken = "fixture-access";
first.SuccessBody = """
    {
        "id": "fixture-user", "username": "Alice", "expiry": 1800000000,
        "admin": true, "accounts_admin": true, "disabled": true, "has_referrals": true,
        "email": {"value": "alice@example.invalid", "enabled": true},
        "discord": {"value": "alice#1234", "enabled": false},
        "telegram": {"value": "alice", "enabled": true},
        "matrix": {"value": "@alice:example.invalid", "enabled": false}
    }
    """;
var details = await client.GetMyDetailsAsync(timeout.Token);
Check(first.LastRequestTarget == "/my/details", "Self-service details used the wrong route.");
Check(details.Id == "fixture-user" && details.Username == "Alice", "Self-service identity was lost.");
Check(details.Expiry == 1800000000 && details.Admin && details.AccountsAdmin && details.Disabled && details.HasReferrals,
    "Self-service account fields were lost.");
Check(details.Email is { Value: "alice@example.invalid", Enabled: true }, "Email contact was not read.");
Check(details.Discord is { Value: "alice#1234", Enabled: false }, "Discord contact was not read.");
Check(details.Telegram is { Value: "alice", Enabled: true }, "Telegram contact was not read.");
Check(details.Matrix is { Value: "@alice:example.invalid", Enabled: false }, "Matrix contact was not read.");
Console.WriteLine("PASS self-service identity, account fields and nested contacts");

foreach (var contacts in new[] { "", ",\"email\":null,\"discord\":null,\"telegram\":null,\"matrix\":null" })
{
    first.SuccessBody = "{\"id\":\"fixture-user\",\"username\":\"Alice\"" + contacts + "}";
    details = await client.GetMyDetailsAsync(timeout.Token);
    Check(details.Username == "Alice", "Username was lost when optional contacts were absent.");
    Check(details.Email is null && details.Discord is null && details.Telegram is null && details.Matrix is null,
        "Absent optional contacts were not preserved.");
}
Console.WriteLine("PASS self-service details with omitted or null contacts");
first.SuccessBody = null;

foreach (var (total, limit, startPage) in new[]
{
    (501, 500, 0), (0, 200, 0), (1, 200, 0), (200, 200, 0),
    (401, 200, 0), (1001, 1000, 0), (7, 2, 2),
})
{
    var received = new ConcurrentQueue<string>();
    first.ResponseFactory = (_, body) =>
    {
        received.Enqueue(body);
        using var document = JsonDocument.Parse(body);
        var page = document.RootElement.GetProperty("page").GetInt32();
        var pageLimit = document.RootElement.GetProperty("limit").GetInt32();
        var users = Enumerable.Range(0, total).Skip(page * pageLimit).Take(pageLimit)
            .Select(index => new { id = $"user-{index}", name = $"User {index}" }).ToArray();
        return ("200 OK", JsonSerializer.Serialize(new { users = users.Length == 0 ? null : users, last_page = users.Length != pageLimit }));
    };
    var search = new UserSearchRequest
    {
        Limit = limit,
        Page = startPage,
        SortByField = "last_active",
        Ascending = false,
        SearchTerms = ["fixture"],
    };
    var users = await client.SearchUsersAsync(search, timeout.Token);
    var expectedIds = Enumerable.Range(0, total).Skip(startPage * limit).Select(index => $"user-{index}");
    Check(users is not null && users.Select(user => user.Id).SequenceEqual(expectedIds),
        $"User search truncated or reordered {total} users at page size {limit}.");
    var expectedRequests = Math.Max(1, (total - startPage * limit) / limit + 1);
    Check(received.Count == expectedRequests, "User search did not stop at the last page.");
    foreach (var (body, index) in received.Select((body, index) => (body, index)))
    {
        using var document = JsonDocument.Parse(body);
        var sent = document.RootElement;
        Check(sent.GetProperty("page").GetInt32() == startPage + index, "User search skipped a page.");
        Check(sent.GetProperty("limit").GetInt32() == limit && sent.GetProperty("sortByField").GetString() == "last_active"
            && !sent.GetProperty("ascending").GetBoolean() && sent.GetProperty("searchTerms")[0].GetString() == "fixture",
            "Pagination changed the search filters or sort order.");
    }
    Check(search.Page == startPage, "User search mutated the caller's request.");
}
first.ResponseFactory = null;
Console.WriteLine("PASS complete user searches across account, announcement and dashboard page sizes");

var failedPages = new ConcurrentQueue<int>();
first.ResponseFactory = (_, body) =>
{
    using var document = JsonDocument.Parse(body);
    var page = document.RootElement.GetProperty("page").GetInt32();
    failedPages.Enqueue(page);
    return page == 0
        ? ("200 OK", "{\"users\":[{\"id\":\"first\",\"name\":\"Alice\"}],\"last_page\":false}")
        : ("500 Internal Server Error", "{\"error\":\"Fixture user search failed\"}");
};
try
{
    await client.SearchUsersAsync(new UserSearchRequest { Limit = 1 }, timeout.Token);
    throw new InvalidOperationException("A later-page failure returned an incomplete user list.");
}
catch (OmnifinApiException)
{
    Check(failedPages.SequenceEqual(new[] { 0, 1 }), "User search failed before requesting the second page.");
}
Console.WriteLine("PASS a later-page error is reported instead of returning partial users");

first.ResponseFactory = (_, _) => ("200 OK", "{\"users\":null,\"last_page\":false}");
try
{
    await client.SearchUsersAsync(new UserSearchRequest(), timeout.Token);
    throw new InvalidOperationException("An empty unfinished page was accepted.");
}
catch (OmnifinApiException)
{
}
Console.WriteLine("PASS empty unfinished pages stop with an error");
first.ResponseFactory = null;

client.ServerBaseAddress = null;
try
{
    await client.LoginAsync("fixture", "correct", timeout.Token);
    throw new InvalidOperationException("An unconfigured server was accepted.");
}
catch (OmnifinApiException)
{
}
Console.WriteLine("PASS missing server returns the normal configuration error");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class LocalApiFixture : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _worker;
    private int _requestCount;
    private string? _lastRequestTarget;
    private string? _successBody;
    private Func<string?, string, (string Status, string Body)>? _responseFactory;

    public Uri BaseAddress { get; }
    public int RequestCount => Volatile.Read(ref _requestCount);
    public string? LastRequestTarget => Volatile.Read(ref _lastRequestTarget);
    public Func<string?, string, (string Status, string Body)>? ResponseFactory
    {
        get => Volatile.Read(ref _responseFactory);
        set => Volatile.Write(ref _responseFactory, value);
    }
    public string? SuccessBody
    {
        get => Volatile.Read(ref _successBody);
        set => Volatile.Write(ref _successBody, value);
    }

    public LocalApiFixture()
    {
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        BaseAddress = new Uri($"http://127.0.0.1:{port}/");
        _worker = ServeAsync();
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                using var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);
                await using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(_stopping.Token);
                var parts = requestLine?.Split(' ', 3);
                Volatile.Write(ref _lastRequestTarget, parts is { Length: >= 2 } ? parts[1] : null);
                string? authorization = null;
                var contentLength = 0;
                var chunked = false;
                while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } line)
                {
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    {
                        authorization = line["Authorization:".Length..].Trim();
                    }
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(line["Content-Length:".Length..].Trim());
                    }
                    if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))
                    {
                        chunked = line.Contains("chunked", StringComparison.OrdinalIgnoreCase);
                    }
                }
                var requestBody = await ReadRequestBodyAsync(reader, contentLength, chunked, _stopping.Token);
                Interlocked.Increment(ref _requestCount);
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("fixture:correct"));
                var accepted = authorization == expected || authorization == "Bearer fixture-access";
                var (status, body) = accepted
                    ? ResponseFactory?.Invoke(LastRequestTarget, requestBody) ?? ("200 OK", SuccessBody ?? "{\"token\":\"fixture-token\"}")
                    : ("401 Unauthorized", "{\"error\":\"Invalid credentials\"}");
                var bytes = Encoding.UTF8.GetBytes(body);
                var headers = $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), _stopping.Token);
                await stream.WriteAsync(bytes, _stopping.Token);
            }
        }
        catch (Exception error) when (_stopping.IsCancellationRequested &&
            error is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
    }

    private static async Task<string> ReadRequestBodyAsync(StreamReader reader, int contentLength, bool chunked, CancellationToken cancellationToken)
    {
        var body = new StringBuilder();
        while (true)
        {
            var length = chunked
                ? Convert.ToInt32((await reader.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException()).Split(';')[0], 16)
                : contentLength;
            if (length == 0)
            {
                if (chunked)
                {
                    while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 }) { }
                }
                return body.ToString();
            }
            var buffer = new char[length];
            if (await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken) != length)
            {
                throw new EndOfStreamException("Incomplete fixture request body.");
            }
            body.Append(buffer);
            if (!chunked)
            {
                return body.ToString();
            }
            await reader.ReadLineAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        await _worker;
        _stopping.Dispose();
    }
}
