namespace Latchkey.Tests;

public class ConcurrencyTests
{
    private static ILatchkey NewInMemory() =>
        LatchkeyFactory.Create(new LatchkeyOptions { ServiceName = "dev.latchkey.test", Backend = LatchkeyBackend.InMemory });

    [Test]
    public async Task Concurrent_DistinctKeys_NoCorruption()
    {
        var c = NewInMemory();
        const int threads = 16;
        const int perThread = 200;

        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int id = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    string key = $"k{id}-{i}";
                    string val = $"v{id}-{i}";
                    c.Set(key, val);
                    if (c.Get(key) != val)
                        throw new InvalidOperationException($"corruption at {key}");
                    if (!c.Delete(key))
                        throw new InvalidOperationException($"missing at delete {key}");
                }
            });
        }

        await Task.WhenAll(tasks); // absence of exceptions is the assertion
    }

    [Test]
    public async Task Concurrent_SameKey_LastWriteWins_NoExceptions()
    {
        var c = NewInMemory();
        const int threads = 16;
        const int perThread = 300;

        var tasks = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            int id = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    c.Set("shared", $"{id}-{i}");
                    _ = c.Get("shared");
                }
            });
        }

        await Task.WhenAll(tasks);

        // Some write won; the store is consistent and readable.
        await Assert.That(c.Get("shared")).IsNotNull();
    }
}
