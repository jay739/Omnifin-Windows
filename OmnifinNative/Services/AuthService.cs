using System.Net;

namespace OmnifinNative.Services;

public sealed class AuthService(OmnifinApiClient apiClient)
{
    private const string CredentialTarget = "OmnifinNative:refresh-token";

    public bool IsAuthenticated => apiClient.AccessToken is not null;
    public bool HasSavedSession => CredentialVault.Read(CredentialTarget) is not null;
    public string? RestoreError { get; private set; }

    public bool IsAdmin
    {
        get => CredentialVault.Read("OmnifinNative:is-admin") == "true";
        private set => CredentialVault.Save("OmnifinNative:is-admin", userName: "is-admin", value ? "true" : "false");
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken)
    {
        RestoreError = null;
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
        catch (OmnifinApiException ex)
        {
            apiClient.AccessToken = null;
            if (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                CredentialVault.Delete(CredentialTarget);
                CredentialVault.Delete("OmnifinNative:is-admin");
                RestoreError = "Your saved session could not be restored. Sign in again.";
            }
            else
            {
                // Preserve the saved session through temporary outages and response errors.
                RestoreError = ex.Message;
            }
            return false;
        }
    }

    public async Task LoginAsync(string username, string password, bool isAdmin, CancellationToken cancellationToken)
    {
        var tokens = isAdmin
            ? await apiClient.LoginAsync(username, password, cancellationToken)
            : await apiClient.LoginUserAsync(username, password, cancellationToken);
        ApplyTokens(tokens);
        IsAdmin = isAdmin;
        RestoreError = null;
    }

    public void Logout()
    {
        RestoreError = null;
        apiClient.AccessToken = null;
        CredentialVault.Delete(CredentialTarget);
        CredentialVault.Delete("OmnifinNative:is-admin");
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
