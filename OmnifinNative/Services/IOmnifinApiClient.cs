using OmnifinNative.Models;

namespace OmnifinNative.Services;

public sealed class OmnifinApiException(string message) : Exception(message);

public interface IOmnifinApiClient
{
    Uri? ServerBaseAddress { get; set; }

    Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken);

    Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<List<RespUser>> SearchUsersAsync(UserSearchRequest request, CancellationToken cancellationToken);

    Task<Dictionary<string, long>> GetWatchTimeAsync(CancellationToken cancellationToken);

    Task EnableDisableUsersAsync(IReadOnlyList<string> userIds, bool enabled, CancellationToken cancellationToken);

    Task ExtendExpiryAsync(IReadOnlyList<string> userIds, int months, int days, CancellationToken cancellationToken);

    Task DeleteUsersAsync(IReadOnlyList<string> userIds, CancellationToken cancellationToken);
}
