using System.Runtime.InteropServices;
using Latchkey.Native;

namespace Latchkey;

/// <summary>
/// Stores secrets in the macOS Keychain as generic passwords via Security.framework. Values are
/// raw bytes in a CFData — no encoding layer. Upsert is SecItemAdd, falling back to SecItemUpdate
/// on a duplicate so ACLs are preserved (never delete-then-add).
/// </summary>
internal sealed unsafe class MacKeychainBackend : ISecretBackend
{
    private static readonly object InitLock = new();
    private static bool _initialized;
    private static bool _available;

    // Resolved once from the loaded frameworks. kSec* are exported CFStringRef symbols; the
    // dictionary callbacks are exported structs whose address we pass straight through.
    private static IntPtr _keyCallBacks;
    private static IntPtr _valueCallBacks;
    private static IntPtr _cfBooleanTrue;
    private static IntPtr _secClass;
    private static IntPtr _secClassGenericPassword;
    private static IntPtr _secAttrService;
    private static IntPtr _secAttrAccount;
    private static IntPtr _secValueData;
    private static IntPtr _secReturnData;
    private static IntPtr _secMatchLimit;
    private static IntPtr _secMatchLimitOne;

    public bool IsAvailable => TryInit();

    public void Store(string service, string key, ReadOnlySpan<byte> value, string label)
    {
        EnsureAvailable();

        using var svc = CFString(service);
        using var acct = CFString(key);
        using var data = CFData(value);
        using var attributes = CreateDictionary();

        IntPtr attrs = attributes.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(attrs, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(attrs, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(attrs, _secAttrAccount, acct.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(attrs, _secValueData, data.DangerousGetHandle());

        int status = Security.SecItemAdd(attrs, IntPtr.Zero);
        if (status == Security.ErrSecDuplicateItem)
        {
            using var query = CreateDictionary();
            IntPtr q = query.DangerousGetHandle();
            CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
            CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
            CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());

            using var changes = CreateDictionary();
            IntPtr ch = changes.DangerousGetHandle();
            CoreFoundation.CFDictionarySetValue(ch, _secValueData, data.DangerousGetHandle());

            status = Security.SecItemUpdate(q, ch);
            if (status != Security.ErrSecSuccess)
                throw new LatchkeyException($"SecItemUpdate failed (OSStatus {status}).");
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

        IntPtr q = query.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secReturnData, _cfBooleanTrue);
        CoreFoundation.CFDictionarySetValue(q, _secMatchLimit, _secMatchLimitOne);

        int status = Security.SecItemCopyMatching(q, out IntPtr result);
        if (status == Security.ErrSecItemNotFound)
            return null;
        if (status != Security.ErrSecSuccess)
            throw new LatchkeyException($"SecItemCopyMatching failed (OSStatus {status}).");

        using var data = new SafeCFTypeHandle(result);
        int length = (int)CoreFoundation.CFDataGetLength(result);
        if (length == 0)
            return [];

        var bytes = new byte[length];
        Marshal.Copy(CoreFoundation.CFDataGetBytePtr(result), bytes, 0, length);
        return bytes;
    }

    public bool Remove(string service, string key)
    {
        EnsureAvailable();

        using var svc = CFString(service);
        using var acct = CFString(key);
        using var query = CreateDictionary();

        IntPtr q = query.DangerousGetHandle();
        CoreFoundation.CFDictionarySetValue(q, _secClass, _secClassGenericPassword);
        CoreFoundation.CFDictionarySetValue(q, _secAttrService, svc.DangerousGetHandle());
        CoreFoundation.CFDictionarySetValue(q, _secAttrAccount, acct.DangerousGetHandle());

        int status = Security.SecItemDelete(q);
        if (status == Security.ErrSecItemNotFound)
            return false;
        if (status == Security.ErrSecSuccess)
            return true;
        throw new LatchkeyException($"SecItemDelete failed (OSStatus {status}).");
    }

    private void EnsureAvailable()
    {
        if (!TryInit())
            throw new LatchkeyBackendUnavailableException(BackendSelector.UnavailableMessage(LatchkeyBackend.MacOSKeychain));
    }

    private static bool TryInit()
    {
        if (_initialized)
            return _available;

        lock (InitLock)
        {
            if (_initialized)
                return _available;

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

    private static IntPtr Deref(IntPtr library, string symbol) =>
        Marshal.ReadIntPtr(NativeLibrary.GetExport(library, symbol));

    private static SafeCFTypeHandle CreateDictionary()
    {
        IntPtr dict = CoreFoundation.CFDictionaryCreateMutable(IntPtr.Zero, 0, _keyCallBacks, _valueCallBacks);
        if (dict == IntPtr.Zero)
            throw new LatchkeyException("CFDictionaryCreateMutable returned null.");
        return new SafeCFTypeHandle(dict);
    }

    private static SafeCFTypeHandle CFString(string value)
    {
        IntPtr cString = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            IntPtr cf = CoreFoundation.CFStringCreateWithCString(IntPtr.Zero, cString, CoreFoundation.KCFStringEncodingUTF8);
            if (cf == IntPtr.Zero)
                throw new LatchkeyException("CFStringCreateWithCString returned null.");
            return new SafeCFTypeHandle(cf);
        }
        finally
        {
            Marshal.FreeCoTaskMem(cString);
        }
    }

    private static SafeCFTypeHandle CFData(ReadOnlySpan<byte> value)
    {
        fixed (byte* p = value)
        {
            IntPtr cf = CoreFoundation.CFDataCreate(IntPtr.Zero, (IntPtr)p, value.Length);
            if (cf == IntPtr.Zero)
                throw new LatchkeyException("CFDataCreate returned null.");
            return new SafeCFTypeHandle(cf);
        }
    }
}
