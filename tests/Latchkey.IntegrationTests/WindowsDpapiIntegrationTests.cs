using System.Text;

using Latchkey.Backends.Dpapi;

namespace Latchkey.IntegrationTests;

/// <summary>
/// The DPAPI backend against real crypt32. Windows only; skipped elsewhere. Uses a throwaway temp
/// directory so nothing leaks into a shared store.
/// </summary>
[Category("Integration")]
public class WindowsDpapiIntegrationTests
{
    static (LatchkeyOptions Options, string Dir) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "latchkey-dpapi-" + Guid.NewGuid().ToString("N"));
        var options = new LatchkeyOptions
        {
            ServiceName = Integration.UniqueService(),
            Backend = LatchkeyBackend.Dpapi,
            BackendOptions =
            [
                DpapiBackendOption.Default with
                {
                    Path = dir
                }
            ]
        };

        return (options, dir);
    }

    [Test]
    public async Task DpapiRoundTripsTextAndBinary()
    {
        Integration.RequireWindows();
        var (options, dir) = Make();
        try
        {
            if (!Latchkey.VerifyPersistence(options))
            {
                Skip.Test("DPAPI is not usable here.");
            }

            var store = LatchkeyFactory.Create(options);
            store.Set("token", "s3cr3t");
            await Assert.That(store.Get("token")).IsEqualTo("s3cr3t");

            byte[] data =
            [
                0x00,
                0x01,
                0xFF,
                0x00,
                0x7F
            ];

            store.Set("bin", data);
            await Assert.That(store.GetBytes("bin")!.SequenceEqual(data)).IsTrue();

            await Assert.That(store.Delete("token")).IsTrue();
            await Assert.That(store.Get("token")).IsNull();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Test]
    public async Task DpapiActuallyEncryptsOnDisk()
    {
        Integration.RequireWindows();
        var (options, dir) = Make();
        try
        {
            if (!Latchkey.VerifyPersistence(options))
            {
                Skip.Test("DPAPI is not usable here.");
            }

            const string secret = "plaintext-should-not-appear";
            LatchkeyFactory.Create(options).Set("k", secret);

            var marker = Encoding.UTF8.GetBytes(secret);
            foreach (var file in Directory.GetFiles(dir))
            {
                var onDisk = File.ReadAllBytes(file);
                await Assert.That(Contains(onDisk, marker)).IsFalse();
            }
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
