namespace Latchkey.Tests;

/// <summary>
/// The Linux base64 layer is pure managed code, so it runs on every OS. It is the guarantee that
/// binary values never reach libsecret as a truncated C string, and that foreign values are
/// rejected instead of returned as mojibake.
/// </summary>
public class SecretServiceCodecTests
{
    [Test]
    public async Task Encode_Then_Decode_RoundTrips_Binary_With_NullBytes()
    {
        byte[] data = { 0x00, 0x01, 0xFF, 0x00, 0x7F, 0x00, 0x80, 0x00 };
        var encoded = SecretServiceCodec.Encode(data);

        // The encoded form is NUL-free ASCII, so libsecret cannot truncate it.
        await Assert.That(encoded.Contains('\0')).IsFalse();

        var decoded = SecretServiceCodec.Decode(encoded);
        await Assert.That(decoded.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task Encode_Empty_RoundTrips()
    {
        var encoded = SecretServiceCodec.Encode(ReadOnlySpan<byte>.Empty);
        var decoded = SecretServiceCodec.Decode(encoded);
        await Assert.That(decoded.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Decode_ForeignNonBase64_Throws_Rather_Than_Returning_Mojibake()
    {
        // A plaintext value written by another tool is not valid base64.
        await Assert.That(() => SecretServiceCodec.Decode("hello world not base64!"))
            .Throws<LatchkeyException>();
    }
}
