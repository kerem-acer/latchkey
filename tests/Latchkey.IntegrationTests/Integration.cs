using System.Runtime.CompilerServices;
using TUnit.Core;

namespace Latchkey.IntegrationTests;

/// <summary>
/// Shared helpers and gating for tests that touch the real OS credential store. Integration tests
/// run by default (<c>dotnet test</c>); exclude them with
/// <c>--treenode-filter "/*/*/*/*[Category!=Integration]"</c>. They skip cleanly where no store
/// exists (headless). The headless-unavailable suite is opt-in via LATCHKEY_EXPECT_UNAVAILABLE=1.
/// </summary>
internal static class Integration
{
    /// <summary>A fresh, unique service namespace so tests never collide or leak into real credentials.</summary>
    public static string UniqueService() => $"dev.latchkey.test.{Guid.NewGuid():N}";

    public static LatchkeyBackend ExpectedBackend()
    {
        if (OperatingSystem.IsWindows()) return LatchkeyBackend.WindowsCredentialManager;
        if (OperatingSystem.IsMacOS()) return LatchkeyBackend.MacOSKeychain;
        return LatchkeyBackend.SecretService;
    }

    /// <summary>Skip when there is no usable OS credential store here (headless/container).</summary>
    public static void RequireBackend()
    {
        if (Latchkey.DetectBackend() is null)
            Skip.Test("No OS credential store is available here (headless/container).");
    }

    public static void RequireMacOS()
    {
        if (!OperatingSystem.IsMacOS())
            Skip.Test("macOS-only test.");
    }

    public static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
            Skip.Test("Windows-only test.");
    }

    /// <summary>The headless failure-path assertions are opt-in — they only pass where there is deliberately no store.</summary>
    public static void RequireExpectUnavailable()
    {
        if (Environment.GetEnvironmentVariable("LATCHKEY_EXPECT_UNAVAILABLE") != "1")
            Skip.Test("Set LATCHKEY_EXPECT_UNAVAILABLE=1 (a store-less environment) to run the unavailable-path assertions.");
    }
}

/// <summary>
/// Runs before the test host's entry point. When the child-write environment variables are set,
/// this process is a spawned child in the cross-process persistence test: it writes one value and
/// exits before any tests run. Ordinary test runs do not set these variables and fall straight through.
/// </summary>
internal static class ChildProcessHook
{
    [ModuleInitializer]
    internal static void MaybeRunAsChildWriter()
    {
        string? service = Environment.GetEnvironmentVariable("LATCHKEY_CHILD_WRITE_SERVICE");
        if (service is null)
            return;

        string key = Environment.GetEnvironmentVariable("LATCHKEY_CHILD_WRITE_KEY")!;
        string value = Environment.GetEnvironmentVariable("LATCHKEY_CHILD_WRITE_VALUE")!;

        var store = LatchkeyFactory.Create(service);
        store.Set(key, value);
        Environment.Exit(0);
    }
}
