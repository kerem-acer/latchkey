using System.Runtime.InteropServices;

namespace Latchkey.Backends.SecretService;

/// <summary>
/// Base64 codec for the Secret Service backend. libsecret stores C strings and truncates at the
/// first NUL, so binary values must be base64-encoded on the way in and decoded on the way out.
/// This layer lives only in this backend — Windows and macOS store raw bytes.
/// </summary>
static class SecretServiceCodec
{
    internal static string Encode(ReadOnlySpan<byte> value) => Convert.ToBase64String(value);

    internal static byte[] Decode(string stored)
    {
        try
        {
            return Convert.FromBase64String(stored);
        }
        catch (FormatException ex)
        {
            throw new LatchkeyException(
                "A value stored under this key is not valid Latchkey data (expected base64). It was " +
                "most likely written by another tool; Latchkey refuses to return possibly-corrupted bytes.",
                ex);
        }
    }
}

/// <summary>
/// Stores secrets in a Linux Secret Service provider (e.g. gnome-keyring) via libsecret, using the
/// non-varargs <c>*v</c> sync functions and a GHashTable of {service, key} attributes.
/// </summary>
sealed class SecretServiceBackend : ISecretBackend
{
    const string SchemaName = "dev.latchkey.Secret";
    const string AttrService = "service";
    const string AttrKey = "key";

    static readonly object InitLock = new();
    static bool _initialized;
    static bool _librariesLoaded;

    static nint _schema; // built SecretSchema*
    static nint _strHash; // g_str_hash
    static nint _strEqual; // g_str_equal
    static nint _schemaNamePtr;
    static nint _attrServicePtr;
    static nint _attrKeyPtr;

    public bool IsAvailable
    {
        get
        {
            if (!EnsureLibraries())
            {
                return false;
            }

            // Probe with an actual lookup of a surely-absent key. A null result with no error means
            // the Secret Service is reachable; a GError means it is not (headless/container/no bus).
            var attrs = BuildAttributes("__latchkey_probe__", "__latchkey_probe__", out var toFree);
            try
            {
                var pw = LibSecret.secret_password_lookupv_sync(
                    _schema,
                    attrs,
                    nint.Zero,
                    out var error);

                using (pw)
                using (error)
                {
                    return error.IsInvalid;
                }
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            finally
            {
                GLib.g_hash_table_destroy(attrs);
                FreeAll(toFree);
            }
        }
    }

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();

        var password = SecretServiceCodec.Encode(value);
        var attrs = BuildAttributes(service, key, out var toFree);
        try
        {
            var ok = LibSecret.secret_password_storev_sync(
                _schema,
                attrs,
                null,
                label,
                password,
                nint.Zero,
                out var error);

            using (error)
            {
                if (!ok)
                {
                    throw new LatchkeyException($"secret_password_store failed: {error.Message ?? "unknown error"}");
                }
            }
        }
        finally
        {
            GLib.g_hash_table_destroy(attrs);
            FreeAll(toFree);
        }
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();

        var attrs = BuildAttributes(service, key, out var toFree);
        try
        {
            var pw = LibSecret.secret_password_lookupv_sync(
                _schema,
                attrs,
                nint.Zero,
                out var error);

            using (pw)
            using (error)
            {
                if (!error.IsInvalid)
                {
                    throw new LatchkeyException($"secret_password_lookup failed: {error.Message ?? "unknown error"}");
                }

                var stored = pw.Value;
                return stored is null ? null : SecretServiceCodec.Decode(stored);
            }
        }
        finally
        {
            GLib.g_hash_table_destroy(attrs);
            FreeAll(toFree);
        }
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();

        var attrs = BuildAttributes(service, key, out var toFree);
        try
        {
            var removed = LibSecret.secret_password_clearv_sync(
                _schema,
                attrs,
                nint.Zero,
                out var error);

            using (error)
            {
                if (!error.IsInvalid)
                {
                    throw new LatchkeyException($"secret_password_clear failed: {error.Message ?? "unknown error"}");
                }

                return removed;
            }
        }
        finally
        {
            GLib.g_hash_table_destroy(attrs);
            FreeAll(toFree);
        }
    }

    void EnsureAvailable()
    {
        if (!EnsureLibraries())
        {
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.SecretService));
        }
    }

    static bool EnsureLibraries()
    {
        if (_initialized)
        {
            return _librariesLoaded;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return _librariesLoaded;
            }

            try
            {
                if (NativeLibrary.TryLoad(GLib.Library, out var glib) &&
                    NativeLibrary.TryLoad(LibSecret.Library, out _))
                {
                    _strHash = NativeLibrary.GetExport(glib, "g_str_hash");
                    _strEqual = NativeLibrary.GetExport(glib, "g_str_equal");
                    BuildSchema();
                    _librariesLoaded = true;
                }
            }
            catch (EntryPointNotFoundException)
            {
                _librariesLoaded = false;
            }

            _initialized = true;
            return _librariesLoaded;
        }
    }

    static unsafe void BuildSchema()
    {
        _schemaNamePtr = Marshal.StringToCoTaskMemUTF8(SchemaName);
        _attrServicePtr = Marshal.StringToCoTaskMemUTF8(AttrService);
        _attrKeyPtr = Marshal.StringToCoTaskMemUTF8(AttrKey);

        // SecretSchema is ~592 bytes on 64-bit; allocate a zeroed, generously sized block and fill
        // the two string attributes. A NULL attribute name terminates the fixed 32-entry array.
        const int schemaSize = 1024;
        var schema = Marshal.AllocHGlobal(schemaSize);
        var p = (byte*)schema;
        new Span<byte>(p, schemaSize).Clear();

        *(nint*)(p + 0) = _schemaNamePtr; // const gchar* name
        *(int*)(p + 8) = LibSecret.SchemaNone; // SecretSchemaFlags flags
        *(nint*)(p + 16) = _attrServicePtr; // attributes[0].name
        *(int*)(p + 24) = LibSecret.AttributeString; // attributes[0].type
        *(nint*)(p + 32) = _attrKeyPtr; // attributes[1].name
        *(int*)(p + 40) = LibSecret.AttributeString; // attributes[1].type
        // attributes[2].name (offset 48) stays NULL -> end of list

        _schema = schema;
    }

    static nint BuildAttributes(string service, string key, out nint[] toFree)
    {
        var table = GLib.g_hash_table_new_full(
            _strHash,
            _strEqual,
            nint.Zero,
            nint.Zero);

        var kService = Marshal.StringToCoTaskMemUTF8(AttrService);
        var vService = Marshal.StringToCoTaskMemUTF8(service);
        var kKey = Marshal.StringToCoTaskMemUTF8(AttrKey);
        var vKey = Marshal.StringToCoTaskMemUTF8(key);

        GLib.g_hash_table_insert(table, kService, vService);
        GLib.g_hash_table_insert(table, kKey, vKey);

        toFree =
        [
            kService,
            vService,
            kKey,
            vKey
        ];

        return table;
    }

    static void FreeAll(nint[] pointers)
    {
        foreach (var p in pointers)
        {
            Marshal.FreeCoTaskMem(p);
        }
    }
}
