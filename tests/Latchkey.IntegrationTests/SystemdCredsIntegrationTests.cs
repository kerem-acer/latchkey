using System.Text;

using Latchkey.Backends.SystemdCreds;

namespace Latchkey.IntegrationTests;

/// <summary>
/// The systemd-creds backend against a real <c>systemd-creds</c>. Skipped when the tool is missing
/// or no TPM/host key is reachable (often requires root). Uses a throwaway temp directory.
/// </summary>
[Category("Integration")]
public class SystemdCredsIntegrationTests
{
    static (LatchkeyOptions Options, string Dir) Make()
    {
        var dir = Path.Combine(Path.GetTempPath(), "latchkey-sdcreds-" + Guid.NewGuid().ToString("N"));
        var options = new LatchkeyOptions
        {
            ServiceName = Integration.UniqueService(),
            Backend = LatchkeyBackend.SystemdCreds,
            BackendOptions =
            [
                SystemdCredsBackendOption.Default with
                {
                    Path = dir
                }
            ]
        };

        return (options, dir);
    }

    [Test]
    public async Task SystemdCredsRoundTripsAndEncryptsOnDisk()
    {
        var (options, dir) = Make();
        try
        {
            if (!Latchkey.VerifyPersistence(options))
            {
                Skip.Test("systemd-creds is not usable here (no tool, TPM, or host-key access).");
            }

            const string secret = "sd-plaintext-should-not-appear";
            var store = LatchkeyFactory.Create(options);
            store.Set("token", secret);
            await Assert.That(store.Get("token")).IsEqualTo(secret);

            var marker = Encoding.UTF8.GetBytes(secret);
            foreach (var file in Directory.GetFiles(dir))
            {
                var onDisk = File.ReadAllBytes(file);
                await Assert.That(Contains(onDisk, marker)).IsFalse();
            }

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
    public async Task SystemdCredsAsyncRoundTripsAndHandlesMissingKeys()
    {
        var (options, dir) = Make();
        try
        {
            if (!Latchkey.VerifyPersistence(options))
            {
                Skip.Test("systemd-creds is not usable here (no tool, TPM, or host-key access).");
            }

            var store = LatchkeyFactory.Create(options);

            // The async path is real I/O here: async file writes/reads around the encrypt/decrypt.
            await store.SetAsync("token", "async-secret");
            await Assert.That(await store.GetAsync("token")).IsEqualTo("async-secret");

            byte[] data =
            [
                0x00,
                0x2A,
                0xFF,
                0x00,
                0x7F
            ];

            await store.SetAsync("bin", data);
            await Assert.That((await store.GetBytesAsync("bin"))!.SequenceEqual(data)).IsTrue();

            // A missing key is not exceptional, sync or async, and decryption is never attempted.
            await Assert.That(await store.GetAsync("missing")).IsNull();
            await Assert.That(store.GetBytes("missing")).IsNull();
            await Assert.That(await store.DeleteAsync("missing")).IsFalse();
            await Assert.That(store.Delete("missing")).IsFalse();

            await Assert.That(await store.DeleteAsync("token")).IsTrue();
            await Assert.That(await store.GetAsync("token")).IsNull();
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
