namespace Latchkey.Tests;

/// <summary>
/// The Windows blob-size limit is enforced in managed code before any P/Invoke, so these run
/// on every OS. Full Windows round-trip behavior is covered by the integration suite in CI.
/// </summary>
public class WindowsBackendTests
{
    [Test]
    public async Task EnsureBlobFits_AtLimit_DoesNotThrow()
    {
        Exception? caught = null;
        try
        {
            WindowsCredentialBackend.EnsureBlobFits(WindowsCredentialBackend.MaxBlobSize);
        }
        catch (Exception e)
        {
            caught = e;
        }

        await Assert.That(caught).IsNull();
    }

    [Test]
    public async Task EnsureBlobFits_OverLimit_Throws()
    {
        await Assert.That(() => WindowsCredentialBackend.EnsureBlobFits(WindowsCredentialBackend.MaxBlobSize + 1))
            .Throws<LatchkeyValueTooLargeException>();
    }

    [Test]
    public async Task Store_OverLimit_Throws_TooLarge_Before_Any_PInvoke()
    {
        var backend = new WindowsCredentialBackend();
        var oversized = new byte[WindowsCredentialBackend.MaxBlobSize + 1];
        await Assert.That(() => backend.Store("dev.latchkey.test", "k", oversized, "label"))
            .Throws<LatchkeyValueTooLargeException>();
    }

    [Test]
    public async Task Other_Backends_Have_No_Size_Limit()
    {
        // InMemory (and by extension mac/linux) accept values well past the Windows cap.
        var c = LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", Backend = LatchkeyBackend.InMemory });
        var big = new byte[WindowsCredentialBackend.MaxBlobSize * 2];
        big[^1] = 0x42;
        c.Set("k", big);
        var read = c.GetBytes("k");
        await Assert.That(read!.Length).IsEqualTo(big.Length);
    }

    [Test]
    public async Task TargetName_Is_Latchkey_Service_Key()
    {
        await Assert.That(WindowsCredentialBackend.TargetName("dev.app", "token")).IsEqualTo("Latchkey:dev.app:token");
    }
}
