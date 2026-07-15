# Latchkey

Store and retrieve secrets in the operating system's native credential store. Set a value, get it back after a restart. Nothing else.

- **Windows** → Credential Manager
- **macOS** → Keychain
- **Linux** → Secret Service (libsecret, e.g. GNOME Keyring)

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
}
```

- `Set` is upsert. There is no separate add/update.
- `Get`/`GetBytes` return `null` for a missing key. **Missing is not an error; it does not throw.**
- `Delete` returns `false` if the key wasn't there. It is idempotent.
- Instances are **thread-safe**; share them, register them as singletons.

### Calls are synchronous and can block

Every call goes straight to the OS. There is no async OS API underneath, so Latchkey offers none — wrapping these in `Task.Run` would be a lie. **A call can block indefinitely if the OS shows the user a keychain-unlock prompt.** Plan for it.

## Diagnostics

```csharp
LatchkeyBackend? backend = Latchkey.Latchkey.DetectBackend();      // what Auto would pick here, or null
bool works = Latchkey.Latchkey.VerifyPersistence("dev.example.myapp"); // round-trips a throwaway value
```

`VerifyPersistence` is how you detect a headless/container environment up front, instead of discovering it at first read.

## What Latchkey does *not* protect you from — read this

Latchkey delegates to the OS. **It invents no cryptography.** Its security is exactly the OS credential store's security, no more.

- **Any process running as your OS user can read your secrets.** There is no cross-application authorization layer. On desktop macOS the Keychain adds a per-app ACL prompt; elsewhere the boundary is simply **OS user isolation.** If malware runs as you, it can read what you stored.
- **At-rest strength is whatever the OS store provides.** Turn on full-disk encryption — **BitLocker**, **FileVault**, **LUKS**. Without it, the on-disk credential store is only as safe as the file permissions around it.
- **Windows caps a value at 2560 bytes** (`CRED_MAX_CREDENTIAL_BLOB_SIZE`). Larger values throw `LatchkeyValueTooLargeException`. Latchkey will **not** silently split a value across credentials — that is a corruption trap on partial writes. Store a key or a reference, not a megabyte.

If those boundaries don't fit your threat model, Latchkey is the wrong tool — and it will tell you so rather than pretend.

## Linux, containers, and headless servers

Linux needs a **Secret Service provider** (e.g. GNOME Keyring) reachable over a **D-Bus session bus.** A plain SSH session, a CI runner, or a container usually has neither.

**When there is no Secret Service, Latchkey throws `LatchkeyBackendUnavailableException`. By design.**

It ships **no encrypted-file fallback, ever.** The reasoning, so nobody has to relitigate it: a fallback file needs a key, and that key would sit on the same disk under the same permissions as the ciphertext. Anyone who can read one can read the other — it defeats `cat` and nothing else. Worse, the caller would get no error and assume their token is in an OS keyring. **A failure that announces itself beats a success that lies.**

The supported answer is that **you** know what secure storage you have when we can't pick one for you — systemd credentials, a TPM, a KMS-derived key, a `pass`/GPG store — so you supply it:

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
    public required string ServiceName { get; set; } // key namespace, reverse-DNS recommended
    public string? DisplayName { get; set; }         // label in Keychain Access / Seahorse
    public LatchkeyBackend Backend { get; set; }      // default Auto
    public ISecretBackend? CustomBackend { get; set; }// bring your own; disables detection
}
```

## How it maps to each OS

| Platform | Store | API | Notes |
|---|---|---|---|
| Windows | Credential Manager | `advapi32` `Cred*W` | `CRED_TYPE_GENERIC`, raw bytes, 2560-byte cap |
| macOS | Keychain | Security.framework `SecItem*` | generic password, raw bytes via CFData |
| Linux | Secret Service | libsecret `*v` sync | attributes `{service, key}`; values base64-encoded so binary survives |

Latchkey stores raw bytes on Windows and macOS. On Linux, libsecret stores C strings and truncates at the first `\0`, so values are base64-encoded there (and only there). A value written under a Latchkey key by another tool that isn't valid base64 is rejected rather than returned as garbage.

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
