using System.Runtime.InteropServices;

namespace Latchkey.Native;

/// <summary>P/Invoke surface for the macOS Keychain (Security.framework).</summary>
internal static partial class Security
{
    internal const string Library = "/System/Library/Frameworks/Security.framework/Security";

    internal const int ErrSecSuccess = 0;
    internal const int ErrSecItemNotFound = -25300;
    internal const int ErrSecDuplicateItem = -25299;

    [LibraryImport(Library)]
    internal static partial int SecItemAdd(IntPtr attributes, IntPtr result);

    [LibraryImport(Library)]
    internal static partial int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [LibraryImport(Library)]
    internal static partial int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [LibraryImport(Library)]
    internal static partial int SecItemDelete(IntPtr query);
}
