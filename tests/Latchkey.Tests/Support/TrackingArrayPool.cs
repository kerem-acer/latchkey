using System.Buffers;

namespace Latchkey.Tests.Support;

/// <summary>
/// An <see cref="ArrayPool{T}"/> that records, at return time, whether each buffer was fully
/// zeroed. Lets tests prove that secret-bearing pooled buffers are wiped before being returned.
/// </summary>
internal sealed class TrackingArrayPool : ArrayPool<byte>
{
    private readonly ArrayPool<byte> _inner = Shared;

    public int RentCount;
    public int ReturnCount;

    /// <summary>True only if every returned buffer was all-zero at the moment it was returned.</summary>
    public bool AllReturnedBuffersWereZeroed { get; private set; } = true;

    /// <summary>True if at least one buffer was actually returned (so tests can assert the path ran).</summary>
    public bool AnyReturned => ReturnCount > 0;

    public override byte[] Rent(int minimumLength)
    {
        RentCount++;
        return _inner.Rent(minimumLength);
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ReturnCount++;

        bool zeroed = true;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != 0)
            {
                zeroed = false;
                break;
            }
        }

        if (!zeroed)
            AllReturnedBuffersWereZeroed = false;

        _inner.Return(array, clearArray);
    }
}
