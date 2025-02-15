using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Aspire.Dashboard.Tools;

internal static class CompareHelpers
{
    // This method is used to compare two keys in a way that avoids timing attacks.
    public static bool CompareKey(byte[] expectedKeyBytes, string requestKey)
    {
        const int StackAllocThreshold = 256;

        var requestByteCount = Encoding.UTF8.GetByteCount(requestKey);

        // A rented array could have previous data. However, we're trimming it to the exact byte count we need.
        // That means all used bytes are overwritten by the following Encoding.GetBytes call.
        byte[]? requestPooled = null;

        Span<byte> requestBytesSpan = requestByteCount switch
        {
            <= StackAllocThreshold => stackalloc byte[StackAllocThreshold],

            _ => requestPooled = ArrayPool<byte>.Shared.Rent(requestByteCount)
        };

        requestBytesSpan = requestBytesSpan.Slice(0, requestByteCount);

        try
        {
            var encodedByteCount = Encoding.UTF8.GetBytes(requestKey, requestBytesSpan);

            Debug.Assert(encodedByteCount == requestBytesSpan.Length, "Should match because span was previously trimmed to byte count value.");

            return CryptographicOperations.FixedTimeEquals(expectedKeyBytes, requestBytesSpan);
        }
        finally
        {
            if (requestPooled != null)
            {
                ArrayPool<byte>.Shared.Return(requestPooled);
            }
        }
    }
}
