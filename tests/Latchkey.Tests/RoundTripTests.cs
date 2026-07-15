using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class RoundTripTests
{
    private static ILatchkey NewInMemory() =>
        LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", Backend = LatchkeyBackend.InMemory });

    [Test]
    [Arguments("ascii value")]
    [Arguments("")]                     // empty string
    [Arguments("a")]                    // 1 byte
    [Arguments("üñïçödé")]              // multi-byte unicode
    [Arguments("emoji 🔐🗝️ mix")]      // surrogate pairs
    [Arguments("line1\nline2\r\nline3")]// embedded newlines
    public async Task String_RoundTrips(string value)
    {
        var c = NewInMemory();
        c.Set("k", value);
        await Assert.That(c.Get("k")).IsEqualTo(value);
    }

    [Test]
    public async Task Get_MissingKey_ReturnsNull_DoesNotThrow()
    {
        var c = NewInMemory();
        await Assert.That(c.Get("missing")).IsNull();
        await Assert.That(c.GetBytes("missing")).IsNull();
    }

    [Test]
    public async Task Delete_MissingKey_ReturnsFalse()
    {
        var c = NewInMemory();
        await Assert.That(c.Delete("missing")).IsFalse();
    }

    [Test]
    public async Task Delete_ExistingKey_ReturnsTrue_ThenGone()
    {
        var c = NewInMemory();
        c.Set("k", "v");
        await Assert.That(c.Delete("k")).IsTrue();
        await Assert.That(c.Get("k")).IsNull();
    }

    [Test]
    public async Task Contains_Reflects_State()
    {
        var c = NewInMemory();
        await Assert.That(c.Contains("k")).IsFalse();
        c.Set("k", "v");
        await Assert.That(c.Contains("k")).IsTrue();
        c.Delete("k");
        await Assert.That(c.Contains("k")).IsFalse();
    }

    [Test]
    public async Task Set_Twice_Upserts_NoDuplicate()
    {
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", CustomBackend = backend });

        c.Set("k", "first");
        c.Set("k", "second");

        await Assert.That(backend.StoreCalls).IsEqualTo(2); // exactly one Store per Set
        await Assert.That(backend.Count).IsEqualTo(1);       // no duplicate entry
        await Assert.That(c.Get("k")).IsEqualTo("second");
    }

    [Test]
    public async Task Binary_WithNullBytes_RoundTrips_ViaGetBytes()
    {
        var c = NewInMemory();
        byte[] data = { 0x00, 0x01, 0xFF, 0x00, 0x7F, 0x00, 0x80 };
        c.Set("k", data);

        var read = c.GetBytes("k");
        await Assert.That(read).IsNotNull();
        await Assert.That(read!.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task Client_Hands_RawBytes_To_Backend_Unencoded()
    {
        // The client must not encode; base64 is a Linux-backend concern only.
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", CustomBackend = backend });

        byte[] data = { 0x00, 0x10, 0x00, 0xAB, 0xCD };
        c.Set("k", data);

        await Assert.That(backend.LastStoredValue!.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task Set_String_Passes_Utf8_Bytes_To_Backend()
    {
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", CustomBackend = backend });

        c.Set("k", "héllo");
        var expected = System.Text.Encoding.UTF8.GetBytes("héllo");
        await Assert.That(backend.LastStoredValue!.SequenceEqual(expected)).IsTrue();
    }
}
