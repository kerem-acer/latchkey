using System.Runtime.InteropServices;

namespace Latchkey.Backends.MacKeychain;

/// <summary>
/// Stores secrets in the macOS Keychain as generic passwords via Security.framework. Values are
/// raw bytes in a CFData — no encoding layer. Upsert is SecItemAdd, falling back to SecItemUpdate
/// on a duplicate so ACLs are preserved (never delete-then-add).
/// </summary>
sealed unsafe class MacKeychainBackend : ISecretBackend
{
    static readonly object InitLock = new();
    static bool _initialized;
    static bool _available;

    // Resolved once from the loaded frameworks. kSec* are exported CFStringRef symbols; the
    // dictionary callbacks are exported structs whose address we pass straight through.
    static nint _keyCallBacks;
    static nint _valueCallBacks;
    static nint _cfBooleanTrue;
    static nint _secClass;
    static nint _secClassGenericPassword;
    static nint _secAttrService;
    static nint _secAttrAccount;
    static nint _secValueData;
    static nint _secReturnData;
    static nint _secMatchLimit;
    static nint _secMatchLimitOne;

    public bool IsAvailable => TryInit();

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label)
    {
        EnsureAvailable();

        using var svc = CFString(service);
        using var acct = CFString(key);
        using var data = CFData(value);
        using var attributes = CreateDictionary();

        var attrs = attributes.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(attrs, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(attrs, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(attrs, _secAttrAccount, acct.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(attrs, _secValueData, data.DangerousGetHandle());

        var status = Security.SecItemAdd(attrs, nint.Zero);
        if (status == Security.ErrSecDuplicateItem)
        {
            using var query = CreateDictionary();
            var q = query.DangerousGetHandle();
            CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
            CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
            CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());

            using var changes = CreateDictionary();
            var ch = changes.DangerousGetHandle();
            CoreFoundation.CFDictionarySetValue(ch, _secValueData, data.DangerousGetHandle());

            status = Security.SecItemUpdate(q, ch);
            if (status != Security.ErrSecSuccess)
            {
                throw new LatchkeyException($"SecItemUpdate failed (OSStatus {status}).");
            }
        }
        else if (status != Security.ErrSecSuccess)
        {
            throw new LatchkeyException($"SecItemAdd failed (OSStatus {status}).");
        }
    }

    public byte[]? Retrieve(string service, string key)
    {
        EnsureAvailable();

        using var svc = CFString(service);
        using var acct = CFString(key);
        using var query = CreateDictionary();

        var q = query.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secReturnData, _cfBooleanTrue);
        CoreFoundation.CFDictionarySetValue(q, _secMatchLimit, _secMatchLimitOne);

        var status = Security.SecItemCopyMatching(q, out var result);
        if (status == Security.ErrSecItemNotFound)
        {
            return null;
        }

        if (status != Security.ErrSecSuccess)
        {
            throw new LatchkeyException($"SecItemCopyMatching failed (OSStatus {status}).");
        }

        using var data = new SafeCFTypeHandle(result);
        var length = (int)CoreFoundation.CFDataGetLength(result);
        if (length == 0)
        {
            return
                [];
        }

        var bytes = new byte[length];
        Marshal.Copy(
            CoreFoundation.CFDataGetBytePtr(result),
            bytes,
            0,
            length);

        return bytes;
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();

        using var svc = CFString(service);
        using var acct = CFString(key);
        using var query = CreateDictionary();

        var q = query.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());

        var status = Security.SecItemDelete(q);
        if (status == Security.ErrSecItemNotFound)
        {
            return false;
        }

        if (status == Security.ErrSecSuccess)
        {
            return true;
        }

        throw new LatchkeyException($"SecItemDelete failed (OSStatus {status}).");
    }

    void EnsureAvailable()
    {
        if (!TryInit())
        {
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.MacOSKeychain));
        }
    }

    static bool TryInit()
    {
        if (_initialized)
        {
            return _available;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return _available;
            }

            try
            {
                if (NativeLibrary.TryLoad(CoreFoundation.Library, out var cf) &&
                    NativeLibrary.TryLoad(Security.Library, out var sec))
                {
                    // Dictionary callback structs: pass the symbol's address straight through.
                    _keyCallBacks = NativeLibrary.GetExport(cf, "kCFTypeDictionaryKeyCallBacks");
                    _valueCallBacks = NativeLibrary.GetExport(cf, "kCFTypeDictionaryValueCallBacks");

                    // CFTypeRef constants: the symbol holds the ref, so dereference it.
                    _cfBooleanTrue = Deref(cf, "kCFBooleanTrue");
                    _secClass = Deref(sec, "kSecClass");
                    _secClassGenericPassword = Deref(sec, "kSecClassGenericPassword");
                    _secAttrService = Deref(sec, "kSecAttrService");
                    _secAttrAccount = Deref(sec, "kSecAttrAccount");
                    _secValueData = Deref(sec, "kSecValueData");
                    _secReturnData = Deref(sec, "kSecReturnData");
                    _secMatchLimit = Deref(sec, "kSecMatchLimit");
                    _secMatchLimitOne = Deref(sec, "kSecMatchLimitOne");

                    _available = true;
                }
            }
            catch (EntryPointNotFoundException)
            {
                _available = false;
            }

            _initialized = true;
            return _available;
        }
    }

    static nint Deref(nint library, string symbol) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));

    static SafeCFTypeHandle CreateDictionary()
    {
        var dict = CoreFoundation.CFDictionaryCreateMutable(
            nint.Zero,
            0,
            _keyCallBacks,
            _valueCallBacks);

        if (dict == nint.Zero)
        {
            throw new LatchkeyException("CFDictionaryCreateMutable returned null.");
        }

        return new SafeCFTypeHandle(dict);
    }

    static SafeCFTypeHandle CFString(string value)
    {
        var cString = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            var cf = CoreFoundation.CFStringCreateWithCString(nint.Zero, cString, CoreFoundation.KCFStringEncodingUTF8);
            if (cf == nint.Zero)
            {
                throw new LatchkeyException("CFStringCreateWithCString returned null.");
            }

            return new SafeCFTypeHandle(cf);
        }
        finally
        {
            Marshal.FreeCoTaskMem(cString);
        }
    }

    static SafeCFTypeHandle CFData(ReadOnlySpan<byte> value)
    {
        fixed (byte* p = value)
        {
            var cf = CoreFoundation.CFDataCreate(nint.Zero, (nint)p, value.Length);
            if (cf == nint.Zero)
            {
                throw new LatchkeyException("CFDataCreate returned null.");
            }

            return new SafeCFTypeHandle(cf);
        }
    }
}
