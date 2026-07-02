namespace OmnifinNative.Services;

public sealed class AuthService(OmnifinApiClient apiClient)
{
    private const string CredentialTarget = "OmnifinNative:refresh-token";

    public bool IsAuthenticated => apiClient.AccessToken is not null;

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken)
    {
        var refreshToken = CredentialVault.Read(CredentialTarget);
        if (refreshToken is null)
        {
            return false;
        }

        try
        {
            var tokens = await apiClient.RefreshAsync(refreshToken, cancellationToken);
            ApplyTokens(tokens);
            return true;
        }
        catch (OmnifinApiException)
        {
            // Refresh token expired (24h) or was invalidated server-side.
            CredentialVault.Delete(CredentialTarget);
            return false;
        }
    }

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var tokens = await apiClient.LoginAsync(username, password, cancellationToken);
        ApplyTokens(tokens);
    }

    public void Logout()
    {
        apiClient.AccessToken = null;
        CredentialVault.Delete(CredentialTarget);
    }

    private void ApplyTokens(Models.TokenResponse tokens)
    {
        apiClient.AccessToken = tokens.Token;
        if (tokens.Refresh is not null)
        {
            CredentialVault.Save(CredentialTarget, userName: "omnifin", tokens.Refresh);
        }
    }
}
