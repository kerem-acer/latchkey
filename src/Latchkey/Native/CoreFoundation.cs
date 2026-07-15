using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Latchkey.Native;

/// <summary>P/Invoke surface for CoreFoundation, used by the macOS Keychain backend.</summary>
internal static partial class CoreFoundation
{
    internal const string Library = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    internal const uint KCFStringEncodingUTF8 = 0x08000100;

    [LibraryImport(Library)]
    internal static partial void CFRelease(IntPtr cf);

    [LibraryImport(Library)]
    internal static partial IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport(Library)]
    internal static partial void CFDictionarySetValue(IntPtr dict, IntPtr key, IntPtr value);

    [LibraryImport(Library)]
    internal static partial IntPtr CFStringCreateWithCString(IntPtr allocator, IntPtr cStr, uint encoding);

    [LibraryImport(Library)]
    internal static partial IntPtr CFDataCreate(IntPtr allocator, IntPtr bytes, nint length);

    [LibraryImport(Library)]
    internal static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport(Library)]
    internal static partial IntPtr CFDataGetBytePtr(IntPtr data);
}

/// <summary>Owns a +1-retained CoreFoundation object; releases it via <c>CFRelease</c>.</summary>
internal sealed class SafeCFTypeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeCFTypeHandle() : base(ownsHandle: true) { }

    public SafeCFTypeHandle(IntPtr handle) : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        CoreFoundation.CFRelease(handle);
        return true;
    }
}
