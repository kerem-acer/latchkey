using System.Security.Cryptography;
using System.Text;

using Latchkey;
using Latchkey.Backends;
using Latchkey.Backends.Files;
using Latchkey.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

Console.OutputEncoding = Encoding.UTF8;

const string service = "dev.latchkey.tour";

// Everything that must PERSIST in this tour uses an explicit File backend under a temp directory, so
// the tour runs identically on your laptop, in CI, and in a headless container — and cleans up after
// itself. File is plaintext: the honest choice for a throwaway demo, and never a keyring. Your real
// app should let `Auto` pick the native OS store (see section 1).
var demoDir = Path.Combine(Path.GetTempPath(), "latchkey-tour-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(demoDir);

try
{
    // 1. Detect the environment ------------------------------------------------------------------
    Section("1. Detect the environment");
    // VerifyPersistence round-trips a throwaway value; it returns false (never throws) when no native
    // store exists, so you can discover a headless/container host up front instead of at first read.
    var nativeWorks = Latchkey.Latchkey.VerifyPersistence(service);
    Console.WriteLine(nativeWorks ? "A native OS credential store is available — in your app, Auto would use it." : "No native store here (headless/container) — Auto would throw; we use an explicit backend.");
    Console.WriteLine($"This tour persists via a File backend at: {demoDir}");

    var store = LatchkeyFactory.Create(
        new LatchkeyOptions
        {
            ServiceName = service,
            Backend = LatchkeyBackend.File,
            BackendOptions =
            [
                FileBackendOption.Default with
                {
                    Path = demoDir
                }
            ]
        });

    // 2. The basics ------------------------------------------------------------------------------
    Section("2. The basics: set, get, contains, delete");
    store.Set("api-token", "s3cr3t"); // upsert — no separate add/update
    Console.WriteLine($"get 'api-token'      -> {store.Get("api-token")}");
    Console.WriteLine($"get 'missing'        -> {store.Get("missing") ?? "(null — missing is not an error)"}");
    Console.WriteLine($"contains 'api-token' -> {store.Contains("api-token")}");
    Console.WriteLine($"delete 'api-token'   -> {store.Delete("api-token")}");
    Console.WriteLine($"delete 'api-token'   -> {store.Delete("api-token")} (idempotent)");

    // 3. Bytes and buffer hygiene ----------------------------------------------------------------
    Section("3. Bytes & buffer hygiene");
    // A string can't be wiped from memory; when you care, use the byte overloads and zero the array
    // yourself. Binary values with an embedded 0x00 round-trip correctly on every backend.
    byte[] secret =
    [
        0x00,
        0x01,
        0x02,
        0xFF,
        0x00
    ];

    store.Set("raw", secret);
    var read = store.GetBytes("raw");
    Console.WriteLine($"raw bytes round-trip -> {read is not null && read.AsSpan().SequenceEqual(secret)}");
    if (read is not null)
    {
        try
        {
            /* use the secret here */
        }
        finally
        {
            CryptographicOperations.ZeroMemory(read);
        } // wipe it when you're done
    }

    store.Delete("raw");

    // 4. Async -----------------------------------------------------------------------------------
    Section("4. Async");
    // File/process/network backends do real async I/O here; native OS stores complete synchronously
    // (an honest sync-completed ValueTask, not Task.Run theatre).
    await store.SetAsync("session", "async-value");
    Console.WriteLine($"await GetAsync       -> {await store.GetAsync("session")}");
    Console.WriteLine($"await DeleteAsync    -> {await store.DeleteAsync("session")}");

    // 5. Per-OS priority and fallback (the Auto path) --------------------------------------------
    Section("5. Per-OS priority & fallback (the Auto path)");
    // Auto consults a per-OS, ordered backend map and uses the first backend that is available:
    //   new BackendMap().For(OSPlatform.Windows, Dpapi).For(OSPlatform.Linux, SystemdCreds).ForAll(File)
    //   resolves to  Windows [Dpapi, File]   macOS [Keychain, File]   Linux [SystemdCreds, File]
    // Below we Clear() the native seed and fall back to File everywhere, so Auto resolves the same on
    // any OS (and doesn't touch your real keychain in this demo).
    var mapped = LatchkeyFactory.Create(
        new LatchkeyOptions
        {
            ServiceName = service,
            Backends = new BackendMap().Clear().ForAll(LatchkeyBackend.File),
            BackendOptions =
            [
                FileBackendOption.Default with
                {
                    Path = demoDir
                }
            ]
        });

    mapped.Set("mapped", "resolved-via-Auto-map");
    Console.WriteLine($"Auto(map) get        -> {mapped.Get("mapped")}");
    mapped.Delete("mapped");

    // 6. Bring your own store --------------------------------------------------------------------
    Section("6. Custom backend (bring your own store)");
    // Implement the public three-method ISecretBackend for any real secret source (KMS, vault, env).
    // CustomBackend wins over everything and skips all platform detection.
    var custom = LatchkeyFactory.Create(
        new LatchkeyOptions
        {
            ServiceName = service,
            CustomBackend = new DictionaryBackend()
        });

    custom.Set("byo", "from-your-own-backend");
    Console.WriteLine($"custom get 'byo'     -> {custom.Get("byo")}");

    // 7. Dependency injection --------------------------------------------------------------------
    Section("7. Dependency injection");
    // Separate package (Latchkey.Extensions.DependencyInjection). ILatchkey registers as a singleton;
    // options are validated at startup, not at first Get.
    using var provider = new ServiceCollection()
        .AddLatchkey(_ => new LatchkeyOptions
        {
            ServiceName = service,
            DisplayName = "Latchkey Tour",
            Backend = LatchkeyBackend.File,
            BackendOptions =
            [
                FileBackendOption.Default with
                {
                    Path = demoDir
                }
            ]
        })
        .BuildServiceProvider();

    var injected = provider.GetRequiredService<ILatchkey>();
    injected.Set("di-key", "resolved-from-di");
    Console.WriteLine($"injected get 'di-key'-> {injected.Get("di-key")}");
    injected.Delete("di-key");

    Console.WriteLine();
    Console.WriteLine("Done. See the README for the full API and the security model.");
}
finally
{
    // Clean up the temp store.
    try
    {
        Directory.Delete(demoDir, true);
    }
    catch (IOException)
    {
        /* best effort */
    }
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

// A minimal in-process ISecretBackend — the shape you'd implement for a real KMS/vault. Not persistent.
sealed file class DictionaryBackend : ISecretBackend
{
    readonly Dictionary<(string Service, string Key), byte[]> _store =
        [];

    public bool IsAvailable => true;

    public void Store(string service,
        string key,
        ReadOnlySpan<byte> value,
        string label) =>
        _store[(service, key)] = value.ToArray();

    public byte[]? Retrieve(string service, string key) =>
        _store.TryGetValue((service, key), out var value) ? value : null;

    public bool Remove(string service, string key) => _store.Remove((service, key));
}
