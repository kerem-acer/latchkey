using System.Runtime.InteropServices;

namespace Latchkey.Backends.MacKeychain;

/// <summary>P/Invoke surface for the macOS Keychain (Security.framework).</summary>
static partial class Security
{
    internal const string Library = "/System/Library/Frameworks/Security.framework/Security";

    internal const int ErrSecSuccess = 0;
    internal const int ErrSecItemNotFound = -25300;
    internal const int ErrSecDuplicateItem = -25299;

    [LibraryImport(Library)]
    internal static partial int SecItemAdd(nint attributes, nint result);

    [LibraryImport(Library)]
    internal static partial int SecItemCopyMatching(nint query, out nint result);

    [LibraryImport(Library)]
    internal static partial int SecItemUpdate(nint query, nint attributesToUpdate);

    [LibraryImport(Library)]
    internal static partial int SecItemDelete(nint query);
}
