using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Latchkey.Backends.SecretService;

/// <summary>P/Invoke surface for GLib (GHashTable and GError), used by the Secret Service backend.</summary>
static partial class GLib
{
    internal const string Library = "libglib-2.0.so.0";

    [LibraryImport(Library, EntryPoint = "g_hash_table_new_full")]
    internal static partial nint g_hash_table_new_full(nint hashFunc,
        nint keyEqualFunc,
        nint keyDestroy,
        nint valueDestroy);

    [LibraryImport(Library, EntryPoint = "g_hash_table_insert")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool g_hash_table_insert(nint table, nint key, nint value);

    [LibraryImport(Library, EntryPoint = "g_hash_table_destroy")]
    internal static partial void g_hash_table_destroy(nint table);

    [LibraryImport(Library, EntryPoint = "g_error_free")]
    internal static partial void g_error_free(nint error);

    /// <summary>
    /// The GError struct. domain (GQuark, 4 bytes) + code (gint, 4 bytes) + message (gchar*),
    /// the message pointer landing at offset 8 on 64-bit after natural alignment.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct GError
    {
        public uint Domain;
        public int Code;
        public IntPtr Message; // gchar*
    }
}

/// <summary>Owns a GError* returned by GLib/libsecret; releases it via <c>g_error_free</c>.</summary>
sealed class SafeGErrorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeGErrorHandle() : base(ownsHandle: true) { }

    /// <summary>Reads the human-readable message from the GError, or null when there is none.</summary>
    internal unsafe string? Message =>
        IsInvalid ? null : Marshal.PtrToStringUTF8(((GLib.GError*)handle)->Message);

    protected override bool ReleaseHandle()
    {
        GLib.g_error_free(handle);
        return true;
    }
}
