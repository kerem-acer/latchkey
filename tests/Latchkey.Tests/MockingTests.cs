using TUnit.Mocks;

namespace Latchkey.Tests;

/// <summary>
/// Demonstrates TUnit.Mocks against <see cref="ISecretBackend"/> for the non-span members.
/// (The <see cref="ISecretBackend.Store"/> overload takes a <see cref="ReadOnlySpan{T}"/>, which
/// no mocking library can intercept; span-based assertions use the hand-written RecordingBackend.)
/// </summary>
public class MockingTests
{
    [Test]
    public async Task Get_Returns_Backend_Bytes_As_Utf8()
    {
        var mock = ISecretBackend.Mock();
        mock.Retrieve(Any<string>(), Any<string>()).Returns(System.Text.Encoding.UTF8.GetBytes("secret"));

        var client = LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            CustomBackend = mock,
        });

        var value = client.Get("k");

        await Assert.That(value).IsEqualTo("secret");
        mock.Retrieve("k", Any<string>()).WasNeverCalled(); // service/key order sanity
    }

    [Test]
    public async Task Delete_Delegates_To_Backend_Remove()
    {
        var mock = ISecretBackend.Mock();
        mock.Remove(Any<string>(), Any<string>()).Returns(true);

        var client = LatchkeyFactory.Create(new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            CustomBackend = mock,
        });

        var deleted = client.Delete("k");

        await Assert.That(deleted).IsTrue();
        mock.Remove(Any<string>(), Any<string>()).WasCalled(Times.Once);
    }
}
