using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class ValidationTests
{
    static ILatchkey NewClient() =>
        LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                CustomBackend = new RecordingBackend()
            });

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("\t")]
    [Arguments("a\0b")]
    public async Task SetRejectsInvalidKey(string key)
    {
        var client = NewClient();
        await Assert.That(() => client.Set(key, "v")).Throws<ArgumentException>();
    }

    [Test]
    public async Task SetRejectsNullKey()
    {
        var client = NewClient();
        await Assert.That(() => client.Set(null!, "v")).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SetRejectsOverLongKey()
    {
        var client = NewClient();
        var key = new string('k', Validation.MaxKeyLength + 1);
        await Assert.That(() => client.Set(key, "v")).Throws<ArgumentException>();
    }

    [Test]
    public async Task SetAcceptsKeyAtExactlyMaxLength()
    {
        var client = NewClient();
        var key = new string('k', Validation.MaxKeyLength);
        client.Set(key, "v");
        await Assert.That(client.Get(key)).IsEqualTo("v");
    }

    [Test]
    [Arguments("simple")]
    [Arguments("with spaces")]
    [Arguments("emoji-🔑")]
    [Arguments("üñïçödé-key")]
    [Arguments("dev.example.app/token")]
    public async Task SetAcceptsValidUnicodeKey(string key)
    {
        var client = NewClient();
        client.Set(key, "v");
        await Assert.That(client.Get(key)).IsEqualTo("v");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("svc\0name")]
    public async Task CreateRejectsInvalidServiceName(string serviceName) => await Assert.That(() => LatchkeyFactory.Create(serviceName)).Throws<ArgumentException>();

    [Test]
    public async Task ValidateServiceNameNullThrowsArgumentNullException() => await Assert.That(() => Validation.ValidateServiceName(null!)).Throws<ArgumentNullException>();

    [Test]
    public async Task ValidateKeyNullThrowsArgumentNullException() => await Assert.That(() => Validation.ValidateKey(null!)).Throws<ArgumentNullException>();

    [Test]
    public async Task CreateRejectsOverLongServiceName()
    {
        var name = new string('s', Validation.MaxServiceNameLength + 1);
        await Assert.That(() => LatchkeyFactory.Create(name)).Throws<ArgumentException>();
    }

    [Test]
    public async Task GetValidatesKey()
    {
        var client = NewClient();
        await Assert.That(() => client.Get("")).Throws<ArgumentException>();
        await Assert.That(() => client.GetBytes("")).Throws<ArgumentException>();
        await Assert.That(() => client.Delete("")).Throws<ArgumentException>();
        await Assert.That(() => client.Contains("")).Throws<ArgumentException>();
    }
}
