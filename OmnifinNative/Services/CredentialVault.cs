using System.Runtime.InteropServices;

namespace OmnifinNative.Services;

// Wraps the Win32 Credential Manager APIs (advapi32.dll) directly rather
// than depending on a third-party wrapper package, since this API surface
// (CRED_TYPE_GENERIC) has been stable since Windows 7 and pulling it in via
// P/Invoke avoids an external dependency for something this security-sensitive.
public static class CredentialVault
{
    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
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
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    public static void Save(string target, string userName, string secret)
    {
        var secretBytes = System.Text.Encoding.Unicode.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = userName,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"CredWrite failed for target '{target}' (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public static string? Read(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var secretBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, secretBytes, 0, secretBytes.Length);
            return System.Text.Encoding.Unicode.GetString(secretBytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public static void Delete(string target)
    {
        CredDelete(target, CRED_TYPE_GENERIC, 0);
    }
}
