namespace Latchkey.IntegrationTests;

/// <summary>
/// The failure path is a feature. On a Linux host with no Secret Service (headless/container), Auto
/// detection must throw a clear, actionable exception rather than silently falling back to a file.
/// Gated behind LATCHKEY_EXPECT_UNAVAILABLE=1, set by the dedicated no-keyring CI job.
/// </summary>
[Category("Unavailable")]
public class UnavailableIntegrationTests
{
    [Test]
    public async Task Auto_On_Headless_Host_Throws_Actionable_Unavailable()
    {
        Integration.RequireExpectUnavailable();

        await Assert.That(() => LatchkeyFactory.Create(Integration.UniqueService()))
            .Throws<LatchkeyBackendUnavailableException>()
            .WithMessageContaining("CustomBackend");
    }

    [Test]
    public async Task DetectBackend_On_Headless_Host_Returns_Null()
    {
        Integration.RequireExpectUnavailable();
        await Assert.That(Latchkey.DetectBackend()).IsNull();
    }

    [Test]
    public async Task VerifyPersistence_On_Headless_Host_Returns_False()
    {
        Integration.RequireExpectUnavailable();
        await Assert.That(Latchkey.VerifyPersistence(Integration.UniqueService())).IsFalse();
    }
}
