using Latchkey.Backends;
using Latchkey.Backends.Files;

namespace Latchkey.Tests;

/// <summary>
/// The plaintext <see cref="LatchkeyBackend.File" /> backend has no external dependency, so it runs
/// on every platform against a throwaway temp directory.
/// </summary>
public class FileBackendTests
{
    static ILatchkey Create(string dir) =>
        LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                Backend = LatchkeyBackend.File,
                BackendOptions =
                [
                    FileBackendOption.Default with
                    {
                        Path = dir
                    }
                ]
            });

    static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), "latchkey-test-" + Guid.NewGuid().ToString("N"));

    [Test]
    [Arguments("ascii")]
    [Arguments("")]
    [Arguments("üñïçödé 🔐")]
    [Arguments("line1\nline2")]
    public async Task StringRoundTrips(string value)
    {
        var dir = NewTempDir();
        try
        {
            var c = Create(dir);
            c.Set("k", value);
            await Assert.That(c.Get("k")).IsEqualTo(value);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BinaryWithNullBytesRoundTrips()
    {
        var dir = NewTempDir();
        try
        {
            var c = Create(dir);
            byte[] data =
            [
                0x00,
                0xFF,
                0x00,
                0x10,
                0x00,
                0x7F
            ];

            c.Set("bin", data);
            var read = c.GetBytes("bin");
            await Assert.That(read).IsNotNull();
            await Assert.That(read!.SequenceEqual(data)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task UpsertOverwritesNoDuplicateFile()
    {
        var dir = NewTempDir();
        try
        {
            var c = Create(dir);
            c.Set("k", "first");
            c.Set("k", "second");
            await Assert.That(c.Get("k")).IsEqualTo("second");
            // One entry file (+ nothing else) for a single key.
            await Assert.That(Directory.GetFiles(dir).Length).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task DeleteIsIdempotent()
    {
        var dir = NewTempDir();
        try
        {
            var c = Create(dir);
            await Assert.That(c.Delete("k")).IsFalse();
            c.Set("k", "v");
            await Assert.That(c.Delete("k")).IsTrue();
            await Assert.That(c.Delete("k")).IsFalse();
            await Assert.That(c.Get("k")).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task PersistsAcrossInstances()
    {
        var dir = NewTempDir();
        try
        {
            Create(dir).Set("k", "durable");
            // A fresh client over the same directory sees the value — proves real persistence.
            await Assert.That(Create(dir).Get("k")).IsEqualTo("durable");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task AsyncRoundTrips()
    {
        var dir = NewTempDir();
        try
        {
            var c = Create(dir);
            await c.SetAsync("k", "async-value");
            await Assert.That(await c.GetAsync("k")).IsEqualTo("async-value");
            await Assert.That(await c.DeleteAsync("k")).IsTrue();
            await Assert.That(await c.GetAsync("k")).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task FileLandsAtHashedPath()
    {
        var dir = NewTempDir();
        try
        {
            Create(dir).Set("api-token", "v");
            var expected = Path.Combine(dir, EntryId.Compute("dev.latchkey.test", "api-token") + ".latchkey");
            await Assert.That(File.Exists(expected)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

public class FileEntryIdTests
{
    [Test]
    public async Task EntryIdIsDeterministicAndFilesystemSafe()
    {
        var a = EntryId.Compute("svc", "key");
        var b = EntryId.Compute("svc", "key");
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a.Contains('/') || a.Contains('+') || a.Contains('=')).IsFalse();
    }

    [Test]
    public async Task EntryIdDistinguishesServiceKeyBoundary() =>
        // "ab"+"c" and "a"+"bc" must not collide.
        await Assert.That(EntryId.Compute("ab", "c")).IsNotEqualTo(EntryId.Compute("a", "bc"));

    [Test]
    public async Task ServiceKeyBoundaryNoCollisionThroughTheBackend()
    {
        // Two clients sharing one directory whose service+key would collide under naive
        // concatenation must round-trip independently.
        var dir = Path.Combine(Path.GetTempPath(), "latchkey-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            LatchkeyFactory.Create(
                new LatchkeyOptions
                {
                    ServiceName = "ab",
                    Backend = LatchkeyBackend.File,
                    BackendOptions =
                    [
                        FileBackendOption.Default with
                        {
                            Path = dir
                        }
                    ]
                }).Set("c", "X");

            LatchkeyFactory.Create(
                new LatchkeyOptions
                {
                    ServiceName = "a",
                    Backend = LatchkeyBackend.File,
                    BackendOptions =
                    [
                        FileBackendOption.Default with
                        {
                            Path = dir
                        }
                    ]
                }).Set("bc", "Y");

            var first = LatchkeyFactory.Create(
                new LatchkeyOptions
                {
                    ServiceName = "ab",
                    Backend = LatchkeyBackend.File,
                    BackendOptions =
                    [
                        FileBackendOption.Default with
                        {
                            Path = dir
                        }
                    ]
                });

            await Assert.That(first.Get("c")).IsEqualTo("X");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
