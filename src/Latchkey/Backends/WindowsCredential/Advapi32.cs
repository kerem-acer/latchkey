using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Latchkey.Backends.WindowsCredential;

/// <summary>P/Invoke surface for the Windows Credential Manager (advapi32).</summary>
static partial class Advapi32
{
    internal const uint CredTypeGeneric = 1; // CRED_TYPE_GENERIC
    internal const uint CredPersistLocalMachine = 2; // CRED_PERSIST_LOCAL_MACHINE
    internal const int MaxCredentialBlobSize = 2560; // CRED_MAX_CREDENTIAL_BLOB_SIZE
    internal const int MaxStringLength = 256; // CRED_MAX_STRING_LENGTH (Comment, TargetAlias)
    internal const int ErrorNotFound = 1168; // ERROR_NOT_FOUND

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "CredReadW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredReadW(string targetName,
        uint type,
        uint flags,
        out SafeCredentialHandle credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredWriteW(in CREDENTIALW credential, uint flags);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "CredDeleteW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CredDeleteW(string targetName, uint type, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    internal static partial void CredFree(nint buffer);
}

/// <summary>
/// The CREDENTIALW struct, blittable so it marshals without per-field string conversion.
/// String and blob fields are raw pointers the backend allocates and frees by hand.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
struct CREDENTIALW
{
    public uint Flags;
    public uint Type;
    public IntPtr TargetName; // LPWSTR
    public IntPtr Comment; // LPWSTR
    public long LastWritten; // FILETIME (two DWORDs)
    public uint CredentialBlobSize;
    public IntPtr CredentialBlob; // LPBYTE
    public uint Persist;
    public uint AttributeCount;
    public IntPtr Attributes;
    public IntPtr TargetAlias; // LPWSTR
    public IntPtr UserName; // LPWSTR
}

/// <summary>Owns the buffer <c>CredReadW</c> allocates; releases it via <c>CredFree</c>.</summary>
sealed class SafeCredentialHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeCredentialHandle() : base(ownsHandle: true) { }

    protected override bool ReleaseHandle()
    {
        Advapi32.CredFree(handle);
        return true;
    }
}
