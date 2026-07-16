# Latchkey

[![CI](https://github.com/kerem-acer/latchkey/actions/workflows/ci.yml/badge.svg)](https://github.com/kerem-acer/latchkey/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Latchkey.svg)](https://www.nuget.org/packages/Latchkey)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Store and retrieve secrets in the operating system's native credential store. Set a value, get it back after a restart. Nothing else.

- **Windows** → Credential Manager
- **macOS** → Keychain
- **Linux** → Secret Service (libsecret, e.g. GNOME Keyring)

That's the default. Encrypted-file, plaintext, `pass`, and `systemd-creds` [backends](#backends-beyond-the-os-default) exist for when you need them — you select those explicitly; `Auto` never picks them for you.

Latchkey is **pure managed code with zero native dependencies of its own** — nothing but source-generated `LibraryImport` P/Invoke against APIs already on the machine. So it's **Native-AOT- and trim-clean**, ships no per-RID binaries, and runs on linux-arm64 with no special build. That's the whole point.

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

- `Set` is upsert — there is no separate add/update.
- `Get`/`GetBytes` return `null` for a missing key. **Missing is not an error; it does not throw.**
- `Delete` returns `false` if the key wasn't there. It is idempotent.
- Instances are **thread-safe** — share them, register them as singletons.

### Bytes, not just strings

`Get` returns a `string` for convenience, but that string **cannot be wiped from memory.** When you care, use the byte overloads and zero the array yourself:

```csharp
byte[]? raw = store.GetBytes("key");
if (raw is not null)
{
    try { Use(raw); }
    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(raw); }
}
```

Binary values with embedded `0x00` round-trip correctly on every platform.

### Sync, async, and blocking — honestly

Native OS stores have **no async API underneath**, so their `*Async` overloads complete synchronously — an honest synchronously-completed `ValueTask`, not `Task.Run` theatre. The async overloads earn their keep on the file/process backends (`File`, `Dpapi`, `Pass`, `SystemdCreds`, or your own), where there is real I/O to await. A *sync* call can block if the OS shows a keychain-unlock prompt; offload it with `Task.Run` at the call site to keep a UI thread responsive.

## Demo

Two runnable samples — start simple, then see the whole surface:

```sh
dotnet run --project samples/Latchkey.QuickStart   # the ~10-line happy path (native store)
dotnet run --project samples/Latchkey.Tour         # bytes, async, backends, custom store, DI — File-backed, runs anywhere
```

## What Latchkey does *not* protect you from — read this

Latchkey delegates to the OS. **It invents no cryptography.** Its security is exactly the OS credential store's security, no more.

- **Any process running as your OS user can read your secrets.** There is no cross-application authorization layer; the boundary is OS user isolation (desktop macOS adds a per-app Keychain ACL prompt). If malware runs as you, it can read what you stored.
- **At-rest strength is whatever the OS store provides.** Turn on full-disk encryption — **BitLocker**, **FileVault**, **LUKS**.
- **Windows caps a value at 2560 bytes** (`CRED_MAX_CREDENTIAL_BLOB_SIZE`); larger values throw `LatchkeyValueTooLargeException`. Latchkey won't silently split a value — store a key or a reference, not a megabyte.

If those boundaries don't fit your threat model, Latchkey is the wrong tool — and it will tell you so rather than pretend.

## Diagnostics

```csharp
bool works = Latchkey.Latchkey.VerifyPersistence("dev.example.myapp"); // round-trips a throwaway value
```

`VerifyPersistence` detects a headless/container environment up front instead of at first read. It returns `false`; it never throws.

## Backends beyond the OS default

`Auto` picks the native store for the current OS and **nothing else** — it will never silently fall back to a file, because a store that quietly appears when the keyring is missing is exactly the "success that lies" we refuse. When *you* know what you want, select one explicitly with `Backend` and configure it through `BackendOptions`:

| `Backend` | What it is | Encrypted at rest? | Needs |
|---|---|---|---|
| `File` | Plaintext files on disk (all OSes) | **No** | a writable directory |
| `Dpapi` | Files encrypted with Windows DPAPI | Yes (OS-managed key) | Windows |
| `Pass` | The `pass` Unix password manager | Yes (your GPG key) | `pass` + a GPG key |
| `SystemdCreds` | Files encrypted with `systemd-creds` | Yes (TPM / host key) | systemd ≥ 250, TPM or host-key access |

```csharp
using Latchkey;
using Latchkey.Backends.Dpapi;

ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName    = "dev.example.myapp",
    Backend        = LatchkeyBackend.Dpapi,
    BackendOptions = [ DpapiBackendOption.Default with { Path = @"C:\ProgramData\MyApp\secrets" } ],
});
```

`BackendOptions` is a bag of typed config — at most one per backend. It **does not select** a backend (`Backend` does); it only parameterizes the one you selected. `FileBackendOption` and `DpapiBackendOption` take a `{ Path }` (DPAPI also a `{ Scope }`, `CurrentUser` by default); `PassBackendOption` takes `{ StoreDir, Prefix }`; `SystemdCredsBackendOption` takes `{ Path, Name }`.

### Per-OS priority and fallback

`Backend = Auto` doesn't hard-code the native store — it consults `Backends`, an OS → ordered backend list seeded with the native default, and uses the **first backend that is available**:

```csharp
ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName = "dev.example.myapp",
    Backends = new BackendMap()                              // starts from the native default
        .For(OSPlatform.Windows, LatchkeyBackend.Dpapi)      // Windows: DPAPI, then the fallback
        .For(OSPlatform.Linux, LatchkeyBackend.SystemdCreds) // Linux: systemd-creds, then fallback
        .ForAll(LatchkeyBackend.File),                       // every OS, last resort: File
});
// resolves to  Windows: [Dpapi, File]   macOS: [Keychain, File]   Linux: [SystemdCreds, File]
```

`For(os, …)` sets one OS's list; `ForAll(…)` appends a universal fallback; `Clear()` drops the native seed. Resolution is first-available-wins, pinned when the instance is created. A specific `Backend` forces that one backend and ignores the map; `CustomBackend` wins over everything.

> **Sharp edge:** a fallback list can resolve to *different* backends on different machines, and a secret written under one backend isn't visible under another. Use fallbacks only where that's acceptable — otherwise pin a single `Backend`.

## Linux, containers, and headless servers

Linux's native `Auto` backend is the Secret Service reachable over a D-Bus session bus — a plain SSH session, a CI runner, or a container usually has neither. **When `Auto` finds no Secret Service, Latchkey throws `LatchkeyBackendUnavailableException`. By design** — it will not quietly write your token to an unencrypted file and let you believe it went to a keyring. On such hosts, select an explicit encrypted backend (`SystemdCreds` fits headless Linux well), or bring your own store:

```csharp
using Latchkey;
using Latchkey.Backends;

public sealed class MyBackend : ISecretBackend        // illustrative; wire up a real source (KMS, vault…)
{
    public bool IsAvailable => true;
    public void Store(string service, string key, ReadOnlySpan<byte> value, string label) { /* … */ }
    public byte[]? Retrieve(string service, string key) => /* … */ null;
    public bool Remove(string service, string key) => false;
}

ILatchkey store = LatchkeyFactory.Create(new LatchkeyOptions
{
    ServiceName   = "dev.example.myapp",
    CustomBackend = new MyBackend(),                  // detection is skipped entirely
});
```

`ISecretBackend` is a public three-method interface (with async defaults you can override). When `CustomBackend` is set, Latchkey uses it directly and runs no platform detection.

## Dependency injection

Separate package — the core `Latchkey` has **zero package dependencies**; pull in `Microsoft.Extensions.*` only if you want this:

```sh
dotnet add package Latchkey.Extensions.DependencyInjection
```

```csharp
services.AddLatchkey("dev.example.myapp");

// or configure options:
services.AddLatchkey(sp => new LatchkeyOptions { ServiceName = "dev.example.myapp", DisplayName = "My App" });

// optional: fail host startup if persistence doesn't actually work
services.AddLatchkeyPersistenceCheck();
```

`ILatchkey` is registered as a **singleton**, and `ServiceName` is validated at **startup** (via `IValidateOptions<>` + `ValidateOnStart`), not at first `Get`.

## Options

```csharp
public sealed class LatchkeyOptions
{
    public required string ServiceName { get; set; }  // key namespace, reverse-DNS recommended
    public string? DisplayName { get; init; }          // label in Keychain Access / Seahorse
    public LatchkeyBackend Backend { get; init; }       // Auto => use Backends; else force one; default Auto
    public BackendMap Backends { get; init; }            // per-OS ordered backend list (Auto path)
    public IReadOnlyList<BackendOption> BackendOptions { get; init; } // typed config for the chosen backend
    public ISecretBackend? CustomBackend { get; init; } // bring your own; wins over everything
}
```

## How it maps to each OS

| Backend | Selected by `Auto`? | API | Notes |
|---|---|---|---|
| Credential Manager | ✅ Windows | `advapi32` `Cred*W` | raw bytes; 2560-byte cap |
| Keychain | ✅ macOS | Security.framework `SecItem*` | generic password, raw bytes via CFData |
| Secret Service | ✅ Linux | libsecret `*v` | attributes `{service, key}`; values base64-encoded so binary survives |
| `File` | opt-in | `System.IO` | one file per key (name = SHA-256 of `{service, key}`); atomic write; **plaintext** |
| `Dpapi` | opt-in (Windows) | `crypt32` `CryptProtectData` | same layout, each value DPAPI-sealed (`CurrentUser` default) |
| `Pass` | opt-in | `pass` (child process) | entries at `{prefix}/{service}/{key}`; values base64-encoded |
| `SystemdCreds` | opt-in (Linux) | `systemd-creds` (child process) | same layout, each value TPM/host-key sealed |

Latchkey stores raw bytes on Windows and macOS. On Linux, libsecret stores C strings and truncates at the first `\0`, so values are base64-encoded there (and only there).

## Building & testing

```sh
dotnet build
dotnet test          # unit + integration by default (integration hits your real OS store)

# unit tests only:
dotnet test -- --treenode-filter "/*/*/*/*[Category!=Integration]"
```

Integration tests touch your real credential store (with unique, cleaned-up service names) and skip where no store exists. CI runs the full suite across Windows, macOS, and Linux; the libraries are `IsAotCompatible`/`IsTrimmable` with the AOT and trim analyzers on, so violations fail the build. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT — see [LICENSE](LICENSE).
