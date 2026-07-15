using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Latchkey.Native;

/// <summary>
/// P/Invoke surface for libsecret. Only the non-varargs (<c>*v</c>) sync variants are used; the
/// varargs forms do not marshal reliably from C#.
/// </summary>
internal static partial class LibSecret
{
    internal const string Library = "libsecret-1.so.0";

    // SecretSchemaAttributeType
    internal const int AttributeString = 0;

    // SecretSchemaFlags
    internal const int SchemaNone = 0;

    [LibraryImport(Library, EntryPoint = "secret_password_storev_sync", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool secret_password_storev_sync(
        IntPtr schema,
        IntPtr attributes,
        string? collection,
        string label,
        string password,
        IntPtr cancellable,
        out SafeGErrorHandle error);

    [LibraryImport(Library, EntryPoint = "secret_password_lookupv_sync")]
    internal static partial SafePasswordHandle secret_password_lookupv_sync(
        IntPtr schema,
        IntPtr attributes,
        IntPtr cancellable,
        out SafeGErrorHandle error);

    [LibraryImport(Library, EntryPoint = "secret_password_clearv_sync")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool secret_password_clearv_sync(
        IntPtr schema,
        IntPtr attributes,
        IntPtr cancellable,
        out SafeGErrorHandle error);

    [LibraryImport(Library, EntryPoint = "secret_password_free")]
    internal static partial void secret_password_free(IntPtr password);
}

/// <summary>Owns a password string returned by libsecret; releases it via <c>secret_password_free</c>.</summary>
internal sealed class SafePasswordHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePasswordHandle() : base(ownsHandle: true) { }

    /// <summary>The stored password as a managed string, or null when the lookup found nothing.</summary>
    internal string? Value => IsInvalid ? null : Marshal.PtrToStringUTF8(handle);

    protected override bool ReleaseHandle()
    {
        LibSecret.secret_password_free(handle);
        return true;
    }
}
