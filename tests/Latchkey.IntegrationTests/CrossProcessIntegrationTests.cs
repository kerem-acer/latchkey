using System.Diagnostics;

namespace Latchkey.IntegrationTests;

/// <summary>
/// The real "survives restarts" requirement: a value written by one process must be readable by a
/// different one. An in-process test cannot prove this, so a child process does the write and exits
/// before this process reads it back.
/// </summary>
[Category("Integration")]
public class CrossProcessIntegrationTests
{
    [Test]
    public async Task ValueWrittenByChildProcessIsReadableByParent()
    {
        Integration.RequireBackend();

        var service = Integration.UniqueService();
        var parent = LatchkeyFactory.Create(service);
        try
        {
            // Re-launch THIS same executable as the child (models an app restart). Using the same
            // binary matters on macOS: Keychain items carry a per-binary ACL, so a different binary
            // could read but not modify/delete what the child created (errSecInvalidOwnerEdit).
            var exe = Environment.ProcessPath ?? "dotnet";
            var isMuxer = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
            var testDll = typeof(CrossProcessIntegrationTests).Assembly.Location;

            var psi = isMuxer ? new ProcessStartInfo(exe, $"exec \"{testDll}\"") : new ProcessStartInfo(exe);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.Environment["LATCHKEY_CHILD_WRITE_SERVICE"] = service;
            psi.Environment["LATCHKEY_CHILD_WRITE_KEY"] = "cross";
            psi.Environment["LATCHKEY_CHILD_WRITE_VALUE"] = "written-by-child";
            // The child must not inherit the parent's own gating, only the child-write instructions.
            psi.Environment.Remove("LATCHKEY_INTEGRATION");

            using var child = Process.Start(psi)!;
            await child.WaitForExitAsync();
            await Assert.That(child.ExitCode).IsEqualTo(0);

            // The child has fully exited; this separate process reads what it stored.
            await Assert.That(parent.Get("cross")).IsEqualTo("written-by-child");
        }
        finally
        {
            // Best-effort cleanup: a delete failing here (e.g. a cross-binary Keychain ACL on macOS)
            // must not fail a test whose persistence assertion already passed.
            try
            {
                parent.Delete("cross");
            }
            catch (LatchkeyException)
            { }
        }
    }
}
