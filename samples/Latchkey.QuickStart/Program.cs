using Latchkey;

// Latchkey stores secrets in the OS-native credential store:
//   Windows -> Credential Manager    macOS -> Keychain    Linux -> Secret Service
// The service name is a namespace for your keys — use reverse-DNS so you don't collide with other apps.
const string service = "dev.latchkey.quickstart";

try
{
    ILatchkey store = LatchkeyFactory.Create(service);   // Auto-detects the native store for this OS

    store.Set("api-token", "s3cr3t");                    // upsert — there is no separate add/update

    string? token = store.Get("api-token");              // "s3cr3t", or null if absent (missing is not an error)
    Console.WriteLine($"stored and read back    : {token}");
    Console.WriteLine($"contains 'api-token'    : {store.Contains("api-token")}");
    Console.WriteLine($"delete 'api-token'      : {store.Delete("api-token")}");   // false if it wasn't there
    Console.WriteLine($"contains after delete   : {store.Contains("api-token")}");
}
catch (LatchkeyBackendUnavailableException ex)
{
    // No native store here (a bare SSH session, a CI runner, a container). Latchkey refuses to
    // silently downgrade to an insecure file — that's the whole point. See the Tour for the
    // explicit File-backed walkthrough that runs anywhere.
    Console.WriteLine($"No native credential store on this machine: {ex.Message}");
    Console.WriteLine("Try the Tour instead:  dotnet run --project samples/Latchkey.Tour");
}
