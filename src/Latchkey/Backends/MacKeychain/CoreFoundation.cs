using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Latchkey.Backends.MacKeychain;

/// <summary>P/Invoke surface for CoreFoundation, used by the macOS Keychain backend.</summary>
static partial class CoreFoundation
{
    internal const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    internal const uint KCFStringEncodingUTF8 = 0x08000100;

    [LibraryImport(Library)]
    internal static partial void CFRelease(nint cf);

    [LibraryImport(Library)]
    internal static partial nint CFDictionaryCreateMutable(nint allocator,
        nint capacity,
        nint keyCallBacks,
        nint valueCallBacks);

    [LibraryImport(Library)]
    internal static partial void CFDictionarySetValue(nint dict, nint key, nint value);

    [LibraryImport(Library)]
    internal static partial nint CFStringCreateWithCString(nint allocator, nint cStr, uint encoding);

    [LibraryImport(Library)]
    internal static partial nint CFDataCreate(nint allocator, nint bytes, nint length);

    [LibraryImport(Library)]
    internal static partial nint CFDataGetLength(nint data);

    [LibraryImport(Library)]
    internal static partial nint CFDataGetBytePtr(nint data);
}

/// <summary>Owns a +1-retained CoreFoundation object; releases it via <c>CFRelease</c>.</summary>
sealed class SafeCFTypeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeCFTypeHandle(nint handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        CoreFoundation.CFRelease(handle);
        return true;
    }
}
