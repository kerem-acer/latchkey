# Latchkey

Store and retrieve secrets in the operating system's native credential store. Set a value, get it back after a restart. Nothing else.

- **Windows** → Credential Manager
- **macOS** → Keychain
- **Linux** → Secret Service (libsecret, e.g. GNOME Keyring)

That's the default. When you need something else — an encrypted file on a headless server, plaintext for dev — there are [opt-in backends](#backends-beyond-the-os-default) you select explicitly. `Auto` never picks them for you.

Latchkey is **pure managed code with zero native dependencies of its own.** It is nothing but source-generated `LibraryImport` P/Invoke against APIs that already exist on the machine. That means it is **Native-AOT-clean and trim-clean**, ships no per-RID C++ binaries, and works on linux-arm64 without a special build. That is the whole point of the library.

```
net8.0  •  net10.0        Windows / macOS / Linux (x64 + arm64)        MIT
```

## Install

```sh
dotnet add package Latchkey
```

## Use

```csharp
using Latchkey;

ILatchkey store = LatchkeyFactory.Create("dev.example.myapp");

store.Set("api-token", "s3cr3t");

string? token = store.Get("api-token");   // "s3cr3t", or null if absent
bool had = store.Delete("api-token");      // false if it wasn't there
bool has = store.Contains("api-token");
```

The service name is a namespace for your keys. Use reverse-DNS (`dev.example.myapp`) so you don't collide with other apps.

### Bytes, not just strings

`Get` returns a `string` for convenience, but that string **cannot be wiped from memory.** When you care, use the byte overloads and zero the array yourself:

```csharp
store.Set("key", stackalloc byte[] { 0x01, 0x02, 0x03 });

byte[]? raw = store.GetBytes("key");
if (raw is not null)
{
    try { Use(raw); }
    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(raw); }
}
```

Binary values with embedded `0x00` round-trip correctly on every platform.

### The whole API

```csharp
public interface ILatchkey
{
    void Set(string key, string value);
    void Set(string key, ReadOnlySpan<byte> value);
    string? Get(string key);
    byte[]? GetBytes(string key);
    bool Delete(string key);
    bool Contains(string key);

    // Async counterparts (ReadOnlyMemory, not Span, since a span can't cross an await):
    ValueTask SetAsync(string key, string value, CancellationToken ct = default);
    ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct = default);
    ValueTask<string?> GetAsync(string key, CancellationToken ct = default);
    ValueTask<byte[]?> GetBytesAsync(string key, CancellationToken ct = default);
    ValueTask<bool> DeleteAsync(string key, CancellationToken ct = default);
    ValueTask<bool> ContainsAsync(string key, CancellationToken ct = default);
}
```

- `Set` is upsert. There is no separate add/update.
- `Get`/`GetBytes` return `null` for a missing key. **Missing is not an error; it does not throw.**
- `Delete` returns `false` if the key wasn't there. It is idempotent.
- Instances are **thread-safe**; share them, register them as singletons.

### Sync, async, and blocking — honestly

The native OS stores (Keychain, Credential Manager, Secret Service) have **no async API underneath**, so their `*Async` overloads complete synchronously — that is honest (a synchronously-completed `ValueTask`), not `Task.Run` theatre. The async overloads earn their keep on the **file-, process-, and network-backed backends** (`File`, `Dpapi`, `Pass`, `SystemdCreds`, or your own `CustomBackend`), where there is real I/O to await.

**A synchronous call can block indefinitely if the OS shows the user a keychain-unlock prompt.** To keep a UI thread responsive against a native store, offload the sync call with `Task.Run` at the call site — that is the honest way to move blocking work off the thread.

## Diagnostics

```csharp
bool works = Latchkey.Latchkey.VerifyPersistence("dev.example.myapp"); // round-trips a throwaway value
```

`VerifyPersistence` is how you detect a headless/container environment up front, instead of discovering it at first read.

## What Latchkey does *not* protect you from — read this

Latchkey delegates to the OS. **It invents no cryptography.** Its security is exactly the OS credential store's security, no more.

- **Any process running as your OS user can read your secrets.** There is no cross-application authorization layer. On desktop macOS the Keychain adds a per-app ACL prompt; elsewhere the boundary is simply **OS user isolation.** If malware runs as you, it can read what you stored.
- **At-rest strength is whatever the OS store provides.** Turn on full-disk encryption — **BitLocker**, **FileVault**, **LUKS**. Without it, the on-disk credential store is only as safe as the file permissions around it.
- **Windows caps a value at 2560 bytes** (`CRED_MAX_CREDENTIAL_BLOB_SIZE`). Larger values throw `LatchkeyValueTooLargeException`. Latchkey will **not** silently split a value across credentials — that is a corruption trap on partial writes. Store a key or a reference, not a megabyte.

If those boundaries don't fit your threat model, Latchkey is the wrong tool — and it will tell you so rather than pretend.

## Backends beyond the OS default

`Auto` (the default) picks the native store for the current OS and **nothing else** — it will never silently pick a file or an encrypted-file backend, because a store that quietly appears when the keyring is missing is exactly the "success that lies" we refuse. But when *you* know what you want, you can select one explicitly with `Backend` and configure it through `BackendOptions`:

| `Backend` | What it is | Encrypted at rest? | Needs |
|---|---|---|---|
| `File` | Plaintext files on disk (all OSes) | **No** | a writable directory |
| `Dpapi` | Files encrypted with Windows DPAPI | Yes (OS-managed key) | Windows |
| `Pass` | The `pass` Unix password manager | Yes (your GPG key) | `pass` + a GPG key |
| `SystemdCreds` | Files encrypted with `systemd-creds` | Yes (TPM / host key) | systemd ≥ 250, TPM or host-key access |

```csharp
using Latchkey;

// Encrypted files on Windows via DPAPI, at a path you choose:
ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName   = "dev.example.myapp",
    Backend       = LatchkeyBackend.Dpapi,
    BackendOptions = [ DpapiBackendOption.Default with { Path = @"C:\ProgramData\MyApp\secrets" } ],
});
```

`BackendOptions` is a bag of typed config — one `BackendOption` per backend at most. It **does not select** a backend (`Backend` does); it only parameterizes the one you selected, and the selected backend reads its matching option (or a default). Anything you don't select is ignored.

- **`FileBackendOption`** `{ Path }` — **plaintext.** Its at-rest safety is only the file permissions and full-disk encryption around the directory. For dev, tests, or when you've secured the file yourself. It never pretends to be a keyring.
- **`DpapiBackendOption`** `{ Path, Scope }` — `Scope` defaults to `CurrentUser` (only your Windows user can decrypt); `LocalMachine` is weaker (any user on the box).
- **`PassBackendOption`** `{ StoreDir, Prefix }` — needs `pass` on `PATH`, an initialised store, and a configured GPG key (gpg-agent handles the unlock prompt).
- **`SystemdCredsBackendOption`** `{ Path, Name }` — needs `systemd-creds` and access to a TPM or the host key (often root).

### Per-OS priority and fallback

`Backend = Auto` (the default) doesn't hard-code the native store — it consults `LatchkeyOptions.Backends`, an **OS → ordered backend list** seeded with the native default (Windows → Credential Manager, macOS → Keychain, Linux → Secret Service). Selection walks the list for the current OS and uses the **first backend that is actually available**. Shape it with a small fluent API:

```csharp
ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName = "dev.example.myapp",
    Backends = new BackendMap()                              // starts from the native default
        .For(OSPlatform.Windows, LatchkeyBackend.Dpapi)      // Windows: DPAPI, then the fallback below
        .For(OSPlatform.Linux, LatchkeyBackend.SystemdCreds) // Linux: systemd-creds, then fallback
        .ForAll(LatchkeyBackend.File),                       // every OS, last resort: File
    BackendOptions = [ DpapiBackendOption.Default with { Path = @"C:\ProgramData\MyApp\secrets" } ],
});
// resolves to  Windows: [Dpapi, File]   macOS: [Keychain, File]   Linux: [SystemdCreds, File]
```

- **`For(os, ...)`** sets the ordered list for one OS (only that OS is overridden; the others keep the native default).
- **`ForAll(...)`** is the all-OS entry — appended *after* the current OS's list as a universal fallback.
- **`Clear()`** drops the native seed to start from scratch.
- Resolution is **first-available-wins**, resolved once and pinned when the instance is created.

A specific `Backend` (anything other than `Auto`) forces that one backend and ignores the map; `CustomBackend` still wins over everything.

> **Sharp edge:** a fallback list can resolve to *different* backends on different machines (e.g. `[Dpapi, File]` lands on File where DPAPI isn't usable), and a secret written under one backend is not visible under another. Use a fallback list only where that's acceptable — otherwise pin a single `Backend`.

Selecting a backend that isn't usable here throws `LatchkeyBackendUnavailableException`, with a message that says what's missing. Use `Latchkey.VerifyPersistence(options)` to check up front.

## Linux, containers, and headless servers

Linux's native `Auto` backend is the **Secret Service** (e.g. GNOME Keyring) reachable over a **D-Bus session bus.** A plain SSH session, a CI runner, or a container usually has neither.

**When `Auto` finds no Secret Service, Latchkey throws `LatchkeyBackendUnavailableException`. By design** — it will not silently downgrade to a file. On such hosts, either select an explicit encrypted backend above (`SystemdCreds` fits headless Linux well), or supply your own store. What Latchkey will never do is *quietly* write your token to an unencrypted file and let you believe it went to a keyring. **A failure that announces itself beats a success that lies.**

If you have a secure source we can't pick for you — a KMS-derived key, a custom vault, a bespoke store — you supply it:

```csharp
using Latchkey;

public sealed class EnvVarBackend : ISecretBackend  // illustrative; use a real source
{
    public bool IsAvailable => true;

    public void Store(string service, string key, ReadOnlySpan<byte> value, string label) =>
        throw new NotSupportedException("read-only source");

    public byte[]? Retrieve(string service, string key)
    {
        var v = Environment.GetEnvironmentVariable($"SECRET_{service}_{key}");
        return v is null ? null : System.Text.Encoding.UTF8.GetBytes(v);
    }

    public bool Remove(string service, string key) => false;
}

ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName   = "dev.example.myapp",
    CustomBackend = new EnvVarBackend(),   // Backend detection is skipped entirely
});
```

`ISecretBackend` is a public, three-method interface. Implement it for whatever real secret source you have. When `CustomBackend` is set, Latchkey uses it directly and runs no platform detection.

## Dependency injection

Separate package — the core `Latchkey` has **zero package dependencies**, and you only pull in `Microsoft.Extensions.*` if you want this:

```sh
dotnet add package Latchkey.Extensions.DependencyInjection
```

```csharp
services.AddLatchkey("dev.example.myapp");

// or configure options:
services.AddLatchkey(options =>
{
    options.ServiceName = "dev.example.myapp";
    options.DisplayName = "My App";
});

// optional: fail host startup if persistence doesn't actually work (can block on an unlock prompt)
services.AddLatchkeyPersistenceCheck();
```

`ILatchkey` is registered as a **singleton** (via `TryAddSingleton`, so repeated calls are safe). `ServiceName` is validated at **startup**, not at first `Get`, via `IValidateOptions<LatchkeyOptions>` + `ValidateOnStart`.

## Options

```csharp
public sealed class LatchkeyOptions
{
    public required string ServiceName { get; set; }  // key namespace, reverse-DNS recommended
    public string? DisplayName { get; set; }          // label in Keychain Access / Seahorse
    public LatchkeyBackend Backend { get; set; }       // Auto => use Backends; else force one; default Auto
    public BackendMap Backends { get; set; }            // per-OS ordered backend list (Auto path)
    public IReadOnlyList<BackendOption> BackendOptions { get; set; } // typed config for the chosen backend
    public ISecretBackend? CustomBackend { get; set; } // bring your own; wins over everything
}
```

## How it maps to each OS

| Platform | `Auto` store | API | Notes |
|---|---|---|---|
| Windows | Credential Manager | `advapi32` `Cred*W` | `CRED_TYPE_GENERIC`, raw bytes, 2560-byte cap |
| macOS | Keychain | Security.framework `SecItem*` | generic password, raw bytes via CFData |
| Linux | Secret Service | libsecret `*v` sync | attributes `{service, key}`; values base64-encoded so binary survives |

Latchkey stores raw bytes on Windows and macOS. On Linux, libsecret stores C strings and truncates at the first `\0`, so values are base64-encoded there (and only there). A value written under a Latchkey key by another tool that isn't valid base64 is rejected rather than returned as garbage.

The opt-in backends (never chosen by `Auto`) map as follows:

| `Backend` | API | Notes |
|---|---|---|
| `File` | `System.IO` | one plaintext file per key, name = SHA-256 of `{service, key}`; atomic write; **unencrypted** |
| `Dpapi` | `crypt32` `CryptProtectData` | same file layout, each value sealed with DPAPI (`CurrentUser` by default); Windows only |
| `Pass` | `pass` (child process) | entries at `{prefix}/{service}/{key}`; values base64-encoded; secrets piped on stdin |
| `SystemdCreds` | `systemd-creds` (child process) | same file layout, each value sealed via `systemd-creds encrypt`; ciphertext stored opaquely |

## Building & testing

```sh
dotnet build
dotnet test          # unit + integration by default (integration hits your real OS store)

# integration tests are tagged [Category("Integration")] — skip them to run unit tests only:
dotnet test -- --treenode-filter "/*/*/*/*[Category!=Integration]"
```

Integration tests run by default and **touch your real credential store** (with unique, cleaned-up
service names); they skip automatically where no store exists (headless). Each native backend is
tested on its own OS — CI runs the integration suite across Windows, macOS, and Linux; there is no
container or emulation path.

The core libraries are marked `IsAotCompatible`/`IsTrimmable` with the AOT and trim analyzers
enabled, so AOT/trim violations are caught at build time.

## License

MIT.
