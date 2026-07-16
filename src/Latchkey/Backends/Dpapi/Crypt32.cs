using System.Runtime.InteropServices;

namespace Latchkey.Backends.Dpapi;

/// <summary>P/Invoke surface for Windows DPAPI (crypt32) plus <c>LocalFree</c> from kernel32.</summary>
static partial class Crypt32
{
    internal const string Library = "crypt32.dll";

    internal const uint CryptProtectLocalMachine = 0x4; // CRYPTPROTECT_LOCAL_MACHINE
    internal const uint CryptProtectUiForbidden = 0x1; // CRYPTPROTECT_UI_FORBIDDEN

    [LibraryImport(Library, EntryPoint = "CryptProtectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptProtectData(
        in DATA_BLOB dataIn,
        nint dataDescr,
        nint optionalEntropy,
        nint reserved,
        nint promptStruct,
        uint flags,
        out DATA_BLOB dataOut);

    [LibraryImport(Library, EntryPoint = "CryptUnprotectData", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CryptUnprotectData(
        in DATA_BLOB dataIn,
        nint dataDescr,
        nint optionalEntropy,
        nint reserved,
        nint promptStruct,
        uint flags,
        out DATA_BLOB dataOut);

    /// <summary>DPAPI allocates the output buffer with LocalAlloc; it must be freed with LocalFree.</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "LocalFree")]
    internal static partial nint LocalFree(nint hMem);
}

/// <summary>The DATA_BLOB struct DPAPI passes data through.</summary>
[StructLayout(LayoutKind.Sequential)]
struct DATA_BLOB
{
    public uint cbData;
    public IntPtr pbData;
}
