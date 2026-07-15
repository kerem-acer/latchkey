using System.Runtime.InteropServices;
using Latchkey.Native;

namespace Latchkey;

/// <summary>
/// Stores secrets in the Windows Credential Manager as CRED_TYPE_GENERIC credentials.
/// Secrets are the raw credential blob — no encoding layer, so the full 2560-byte budget
/// is usable. Values above that hard limit throw rather than being silently chunked.
/// </summary>
internal sealed unsafe class WindowsCredentialBackend : ISecretBackend
{
    /// <summary>CRED_MAX_CREDENTIAL_BLOB_SIZE. Values above this cannot be stored.</summary>
    internal const int MaxBlobSize = Advapi32.MaxCredentialBlobSize;

    public bool IsAvailable
    {
        get
        {
            try
            {
                // Probe the API with a surely-absent target. Success or ERROR_NOT_FOUND both
                // prove advapi32 is usable; a missing library throws and means "not available".
                _ = Advapi32.CredReadW("Latchkey:__availability_probe__", Advapi32.CredTypeGeneric, 0, out var handle);
                handle.Dispose();
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }

    public void Store(string service, string key, ReadOnlySpan<byte> value, string label)
    {
        EnsureBlobFits(value.Length);

        string target = TargetName(service, key);
        string comment = Truncate(label, Advapi32.MaxStringLength);

        IntPtr targetPtr = Marshal.StringToHGlobalUni(target);
        IntPtr commentPtr = Marshal.StringToHGlobalUni(comment);
        IntPtr blobPtr = value.Length == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(value.Length);
        try
        {
            if (value.Length > 0)
            {
                fixed (byte* src = value)
                {
                    Buffer.MemoryCopy(src, (void*)blobPtr, value.Length, value.Length);
                }
            }

            var cred = new CREDENTIALW
            {
                Type = Advapi32.CredTypeGeneric,
                TargetName = targetPtr,
                Comment = commentPtr,
                CredentialBlobSize = (uint)value.Length,
                CredentialBlob = blobPtr,
                Persist = Advapi32.CredPersistLocalMachine,
            };

            if (!Advapi32.CredWriteW(in cred, 0))
            {
                int err = Marshal.GetLastPInvokeError();
                throw new LatchkeyException($"CredWrite failed (Win32 error {err}) for target '{target}'.");
            }
        }
        finally
        {
            if (blobPtr != IntPtr.Zero)
            {
                // Wipe the secret from unmanaged memory before releasing it.
                new Span<byte>((void*)blobPtr, value.Length).Clear();
                Marshal.FreeHGlobal(blobPtr);
            }
            Marshal.FreeHGlobal(targetPtr);
            Marshal.FreeHGlobal(commentPtr);
        }
    }

    public byte[]? Retrieve(string service, string key)
    {
        string target = TargetName(service, key);

        if (!Advapi32.CredReadW(target, Advapi32.CredTypeGeneric, 0, out var handle))
        {
            int err = Marshal.GetLastPInvokeError();
            handle.Dispose();
            if (err == Advapi32.ErrorNotFound)
                return null;
            throw new LatchkeyException($"CredRead failed (Win32 error {err}) for target '{target}'.");
        }

        using (handle)
        {
            var cred = (CREDENTIALW*)handle.DangerousGetHandle();
            int size = (int)cred->CredentialBlobSize;
            if (size == 0)
                return [];

            var result = new byte[size];
            Marshal.Copy((IntPtr)cred->CredentialBlob, result, 0, size);
            return result;
        }
    }

    public bool Remove(string service, string key)
    {
        string target = TargetName(service, key);

        if (Advapi32.CredDeleteW(target, Advapi32.CredTypeGeneric, 0))
            return true;

        int err = Marshal.GetLastPInvokeError();
        if (err == Advapi32.ErrorNotFound)
            return false;
        throw new LatchkeyException($"CredDelete failed (Win32 error {err}) for target '{target}'.");
    }

    /// <summary>Composes the user-visible credential target name.</summary>
    internal static string TargetName(string service, string key) => $"Latchkey:{service}:{key}";

    /// <summary>Guards the Windows blob-size limit. Extracted so it is unit-testable off-platform.</summary>
    internal static void EnsureBlobFits(int length)
    {
        if (length > MaxBlobSize)
            throw new LatchkeyValueTooLargeException(
                $"Value is {length} bytes; Windows Credential Manager allows at most {MaxBlobSize} bytes per credential.");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
