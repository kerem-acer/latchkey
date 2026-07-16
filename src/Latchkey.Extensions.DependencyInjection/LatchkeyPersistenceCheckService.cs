using System.Security.Cryptography;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection;

/// <summary>
/// Optional startup check: round-trips a throwaway value through the <b>configured</b> backend and
/// fails host startup if it does not persist. Registered by
/// <see cref="LatchkeyServiceCollectionExtensions.AddLatchkeyPersistenceCheck" />.
/// </summary>
sealed class LatchkeyPersistenceCheckService : IHostedService
{
    readonly ILatchkey _latchkey;
    readonly string _serviceName;

    public LatchkeyPersistenceCheckService(ILatchkey latchkey, IOptions<LatchkeyOptions> options)
    {
        _latchkey = latchkey;
        _serviceName = options.Value.ServiceName;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var probeKey = "__latchkey_persistence_check_" + Guid.NewGuid().ToString("N");
        Span<byte> probe = stackalloc byte[16];
        RandomNumberGenerator.Fill(probe);
        try
        {
            _latchkey.Set(probeKey, probe);
            var read = _latchkey.GetBytes(probeKey);
            var roundTripped = read is not null && read.AsSpan().SequenceEqual(probe);
            if (read is not null)
            {
                CryptographicOperations.ZeroMemory(read);
            }

            if (!roundTripped)
            {
                throw new LatchkeyBackendUnavailableException(
                    $"Latchkey persistence check failed for service '{_serviceName}': the configured store did " +
                    "not round-trip a value. See LatchkeyOptions.CustomBackend for headless/container hosts.");
            }
        }
        catch (LatchkeyException ex) when (ex is not LatchkeyBackendUnavailableException)
        {
            throw new LatchkeyBackendUnavailableException(
                $"Latchkey persistence check failed for service '{_serviceName}': {ex.Message}",
                ex);
        }
        finally
        {
            try
            {
                _latchkey.Delete(probeKey);
            }
            catch (LatchkeyException)
            {
                // Best-effort cleanup of the probe key.
            }

            CryptographicOperations.ZeroMemory(probe);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
