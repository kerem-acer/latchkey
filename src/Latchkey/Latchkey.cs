using System.Security.Cryptography;

namespace Latchkey;

/// <summary>Diagnostics for verifying credential-store availability.</summary>
public static class Latchkey
{
    /// <summary>
    /// Round-trips a throwaway value through the auto-detected backend to prove that
    /// persistence actually works. Returns <c>false</c> — rather than throwing — when no
    /// usable store is present, so callers can detect headless/container environments up
    /// front instead of at first read.
    /// </summary>
    public static bool VerifyPersistence(string serviceName)
    {
        Validation.ValidateServiceName(serviceName);
        return VerifyPersistence(
            new LatchkeyOptions
            {
                ServiceName = serviceName
            });
    }

    /// <summary>
    /// Round-trips a throwaway value through the backend selected by <paramref name="options" />
    /// to prove that persistence actually works. Returns <c>false</c> — rather than throwing —
    /// when the selected store is not usable.
    /// </summary>
    public static bool VerifyPersistence(LatchkeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        ILatchkey store;
        try
        {
            store = LatchkeyFactory.Create(options);
        }
        catch (LatchkeyException)
        {
            return false;
        }

        var probeKey = "__latchkey_probe_" + Guid.NewGuid().ToString("N");
        Span<byte> probe = stackalloc byte[16];
        RandomNumberGenerator.Fill(probe);
        try
        {
            store.Set(probeKey, probe);
            var read = store.GetBytes(probeKey);
            try
            {
                return read is not null && read.AsSpan().SequenceEqual(probe);
            }
            finally
            {
                if (read is not null)
                {
                    CryptographicOperations.ZeroMemory(read);
                }
            }
        }
        catch (LatchkeyException)
        {
            return false;
        }
        finally
        {
            try
            {
                store.Delete(probeKey);
            }
            catch (LatchkeyException)
            {
                // Best-effort cleanup of the probe key.
            }

            CryptographicOperations.ZeroMemory(probe);
        }
    }
}
