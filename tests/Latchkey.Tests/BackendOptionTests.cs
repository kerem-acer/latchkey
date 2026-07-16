using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class BackendOptionTests
{
    [Test]
    public async Task DuplicateOptionForSameBackendThrows()
    {
        var options = new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.File,
            BackendOptions =
            [
                FileBackendOption.Default with
                {
                    Path = "/a"
                },
                FileBackendOption.Default with
                {
                    Path = "/b"
                }
            ]
        };

        await Assert.That(() => LatchkeyFactory.Create(options)).Throws<ArgumentException>();
    }

    [Test]
    public async Task NullOptionEntryThrows()
    {
        var options = new LatchkeyOptions
        {
            ServiceName = "dev.latchkey.test",
            Backend = LatchkeyBackend.File,
            BackendOptions =
            [
                null!
            ]
        };

        await Assert.That(() => LatchkeyFactory.Create(options)).Throws<ArgumentException>();
    }

    [Test]
    public async Task DifferentBackendOptionsCoexist()
    {
        // A file option plus a DPAPI option is fine — only the selected backend reads its own.
        var dir = Path.Combine(Path.GetTempPath(), "latchkey-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var c = LatchkeyFactory.Create(
                new LatchkeyOptions
                {
                    ServiceName = "dev.latchkey.test",
                    Backend = LatchkeyBackend.File,
                    BackendOptions =
                    [
                        FileBackendOption.Default with
                        {
                            Path = dir
                        },
                        DpapiBackendOption.Default with
                        {
                            Scope = DpapiScope.LocalMachine
                        }
                    ]
                });

            c.Set("k", "v");
            await Assert.That(c.Get("k")).IsEqualTo("v");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BackendOptionsIgnoredWhenCustomBackendSet()
    {
        // CustomBackend wins outright; a bogus duplicate list would otherwise throw, but it is not read.
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                CustomBackend = backend,
                BackendOptions =
                [
                    FileBackendOption.Default with
                    {
                        Path = "/unused"
                    }
                ]
            });

        c.Set("k", "v");
        await Assert.That(backend.StoreCalls).IsEqualTo(1);
    }
}
