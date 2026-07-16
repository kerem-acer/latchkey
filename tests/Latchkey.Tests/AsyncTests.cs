using Latchkey.Backends;
using Latchkey.Tests.Support;

namespace Latchkey.Tests;

public class AsyncTests
{
    static ILatchkey NewInMemory() =>
        LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                Backend = LatchkeyBackend.InMemory
            });

    [Test]
    public async Task AsyncRoundTripsStringAndBytes()
    {
        var c = NewInMemory();

        await c.SetAsync("s", "value");
        await Assert.That(await c.GetAsync("s")).IsEqualTo("value");

        byte[] data =
        [
            0x00,
            0x01,
            0xFF,
            0x00
        ];

        await c.SetAsync("b", data);
        var read = await c.GetBytesAsync("b");
        await Assert.That(read).IsNotNull();
        await Assert.That(read!.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task AsyncMissingDeleteContainsSemantics()
    {
        var c = NewInMemory();

        await Assert.That(await c.GetAsync("missing")).IsNull();
        await Assert.That(await c.GetBytesAsync("missing")).IsNull();
        await Assert.That(await c.ContainsAsync("missing")).IsFalse();
        await Assert.That(await c.DeleteAsync("missing")).IsFalse();

        await c.SetAsync("k", "v");
        await Assert.That(await c.ContainsAsync("k")).IsTrue();
        await Assert.That(await c.DeleteAsync("k")).IsTrue();
        await Assert.That(await c.ContainsAsync("k")).IsFalse();
    }

    [Test]
    public async Task SetAsyncStringZeroesPooledBufferBeforeReturn()
    {
        var pool = new TrackingArrayPool();
        var client = new LatchkeyClient(
            new RecordingBackend(),
            "dev.latchkey.test",
            "dev.latchkey.test",
            pool);

        // The async path cannot stackalloc across the await, so it always rents.
        await client.SetAsync("k", "small value");

        await Assert.That(pool.AnyReturned).IsTrue();
        await Assert.That(pool.AllReturnedBuffersWereZeroed).IsTrue();
    }

    [Test]
    public async Task SetAsyncBytesPassesRawBytesToBackend()
    {
        var backend = new RecordingBackend();
        var c = LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                CustomBackend = backend
            });

        byte[] data =
        [
            0x00,
            0x10,
            0xAB,
            0xCD
        ];

        await c.SetAsync("k", data);

        await Assert.That(backend.LastStoredValue!.SequenceEqual(data)).IsTrue();
    }

    [Test]
    public async Task SyncOnlyBackendAsyncDefaultsCompleteSynchronously()
    {
        // A backend that implements only the sync contract inherits the default-interface async
        // methods, which must complete synchronously (an honest sync-completed ValueTask, not Task.Run).
        ISecretBackend backend = new RecordingBackend();
        backend.Store(
            "s",
            "k",
            [
                1,
                2,
                3
            ],
            "label");

        var retrieve = backend.RetrieveAsync("s", "k");
        await Assert.That(retrieve.IsCompleted).IsTrue();
        var value = await retrieve;
        await Assert.That(
            value!.SequenceEqual(
                new byte[]
                {
                    1,
                    2,
                    3
                })).IsTrue();

        var store = backend.StoreAsync(
            "s",
            "k2",
            "\t"u8.ToArray(),
            "label");

        await Assert.That(store.IsCompleted).IsTrue();
        await store;

        var remove = backend.RemoveAsync("s", "k");
        await Assert.That(remove.IsCompleted).IsTrue();
        await Assert.That(await remove).IsTrue();
    }

    [Test]
    public async Task AsyncOverridesAreReachedThroughTheClient()
    {
        // The client must call the backend's async methods, not the sync ones.
        var backend = new AsyncRecordingBackend();
        var c = LatchkeyFactory.Create(
            new LatchkeyOptions
            {
                ServiceName = "dev.latchkey.test",
                CustomBackend = backend
            });

        await c.SetAsync("k", "v");
        await Assert.That(await c.GetAsync("k")).IsEqualTo("v");
        await Assert.That(await c.DeleteAsync("k")).IsTrue();

        await Assert.That(backend.AsyncStoreCalls).IsEqualTo(1);
        await Assert.That(backend.AsyncRetrieveCalls >= 1).IsTrue();
        await Assert.That(backend.AsyncRemoveCalls).IsEqualTo(1);
        await Assert.That(backend.SyncCalls).IsEqualTo(0);
    }
}
