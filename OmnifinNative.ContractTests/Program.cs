using System.Net;
using System.Net.Sockets;
using System.Text;
using OmnifinNative.Services;

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

    public Uri BaseAddress { get; }
    public int RequestCount => Volatile.Read(ref _requestCount);
    public string? LastRequestTarget => Volatile.Read(ref _lastRequestTarget);

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
                while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } line)
                {
                    if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    {
                        authorization = line["Authorization:".Length..].Trim();
                    }
                }
                Interlocked.Increment(ref _requestCount);
                var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("fixture:correct"));
                var accepted = authorization == expected || authorization == "Bearer fixture-access";
                var body = accepted ? "{\"token\":\"fixture-token\"}" : "{\"error\":\"Invalid credentials\"}";
                var bytes = Encoding.UTF8.GetBytes(body);
                var status = accepted ? "200 OK" : "401 Unauthorized";
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

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        await _worker;
        _stopping.Dispose();
    }
}
