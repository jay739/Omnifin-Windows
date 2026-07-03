Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class CredManager {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    public static string Read(string target) {
        IntPtr credPtr;
        if (!CredRead(target, 1, 0, out credPtr)) {
            return null;
        }
        try {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) {
                return null;
            }
            byte[] bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        } finally {
            CredFree(credPtr);
        }
    }
}
"@

$serverUrl = [CredManager]::Read("OmnifinNative:server-url")
$refreshToken = [CredManager]::Read("OmnifinNative:refresh-token")

if ($null -eq $serverUrl) {
    Write-Host "No server URL found."
    exit
}

if ($null -eq $refreshToken) {
    Write-Host "No refresh token found. User is not signed in."
    exit
}

if ($serverUrl.EndsWith("/")) {
    $serverUrl = $serverUrl.Substring(0, $serverUrl.Length - 1)
}

# Now query /token/refresh to get an access token
$refreshUrl = "$serverUrl/token/refresh"
try {
    $headers = @{
        "Authorization" = "Bearer $refreshToken"
    }
    $tokens = Invoke-RestMethod -Uri $refreshUrl -Method Get -Headers $headers
    $accessToken = $tokens.token
    
    # Query /config
    $configUrl = "$serverUrl/config"
    $headers = @{
        "Authorization" = "Bearer $accessToken"
    }
    $configResponse = Invoke-RestMethod -Uri $configUrl -Method Get -Headers $headers
    Write-Host "Config JSON:"
    Write-Host ($configResponse | ConvertTo-Json -Depth 5)

} catch {
    Write-Error $_.Exception.Message
}
