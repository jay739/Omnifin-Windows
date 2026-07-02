namespace OmnifinNative.Services;

// The server URL isn't secret, but it's stored alongside the auth tokens in
// CredentialVault so there's a single place session state lives and clears
// together (e.g. on logout / "forget server").
public static class ServerSettings
{
    private const string CredentialTarget = "OmnifinNative:server-url";

    public static Uri? LoadServerUrl()
    {
        var raw = CredentialVault.Read(CredentialTarget);
        return raw is not null && Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
    }

    public static void SaveServerUrl(Uri serverUrl) =>
        CredentialVault.Save(CredentialTarget, userName: "server-url", serverUrl.ToString());

    public static void ClearServerUrl() => CredentialVault.Delete(CredentialTarget);
}
