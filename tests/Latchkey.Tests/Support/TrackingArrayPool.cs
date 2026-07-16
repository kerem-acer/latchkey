using System.Buffers;

namespace Latchkey.Tests.Support;

/// <summary>
/// An <see cref="ArrayPool{T}" /> that records, at return time, whether each buffer was fully
/// zeroed. Lets tests prove that secret-bearing pooled buffers are wiped before being returned.
/// </summary>
/// <remarks>
/// It deliberately does <b>not</b> delegate to <see cref="ArrayPool{T}.Shared" />. The shared pool
/// rounds a rental up to a bucket size, so the buffer is larger than requested and its tail holds
/// leftover bytes from earlier rentals across the whole process — bytes the client is not responsible
/// for wiping. Checking that oversized, shared tail made the zeroing assertion flaky (it passed or
/// failed depending on unrelated pool history and parallel-test interleaving). Instead this pool hands
/// out an <em>exact-sized</em> buffer pre-filled with a non-zero sentinel: the whole buffer is exactly
/// the region the client must wipe, so a missed byte surfaces deterministically as a leftover sentinel.
/// </remarks>
sealed class TrackingArrayPool : ArrayPool<byte>
{
    const byte Sentinel = 0xFF;

    public int RentCount;
    public int ReturnCount;

    /// <summary>True only if every returned buffer was all-zero at the moment it was returned.</summary>
    public bool AllReturnedBuffersWereZeroed { get; private set; } = true;

    /// <summary>True if at least one buffer was actually returned (so tests can assert the path ran).</summary>
    public bool AnyReturned => ReturnCount > 0;

    public override byte[] Rent(int minimumLength)
    {
        RentCount++;

        var buffer = new byte[minimumLength];
        Array.Fill(buffer, Sentinel);
        return buffer;
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ReturnCount++;

        var zeroed = true;
        for (var i = 0; i < array.Length; i++)
        {
            if (array[i] != 0)
            {
                zeroed = false;
                break;
            }
        }

        if (!zeroed)
        {
            AllReturnedBuffersWereZeroed = false;
        }
    }
}
