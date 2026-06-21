using System.Buffers.Binary;

namespace ClassCast.Common.Protocol;

/// <summary>
/// Reads length-prefixed frames written in the ClassCast wire format
/// (<c>[ 4-byte big-endian length ][ payload ]</c>) from a stream. The
/// counterpart of <see cref="FrameWriter"/>.
/// </summary>
public static class FrameReader
{
    /// <summary>
    /// Maximum frame size accepted, as a guard against malformed length prefixes.
    /// 64 MB comfortably exceeds any legitimate JPEG frame or JSON control message.
    /// </summary>
    public const int MaxFrameLength = 64 * 1024 * 1024;

    /// <summary>
    /// Asynchronously reads a single length-prefixed frame from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The source stream (typically a <c>NetworkStream</c>).</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The payload bytes, or <c>null</c> if the stream reached end-of-stream cleanly
    /// (i.e. the connection was closed) before a complete frame was read.
    /// </returns>
    /// <exception cref="InvalidDataException">The length prefix is negative or exceeds <see cref="MaxFrameLength"/>.</exception>
    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];
        if (!await ReadExactlyOrNullAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length < 0 || length > MaxFrameLength)
        {
            throw new InvalidDataException($"Frame length {length} is out of the permitted range (0..{MaxFrameLength}).");
        }

        if (length == 0)
        {
            return [];
        }

        byte[] payload = new byte[length];
        if (!await ReadExactlyOrNullAsync(stream, payload, cancellationToken).ConfigureAwait(false))
        {
            // Stream closed mid-frame: treat as a clean disconnect.
            return null;
        }

        return payload;
    }

    /// <summary>
    /// Reads a length-prefixed frame and deserialises it as a <see cref="ControlMessage"/>.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>
    /// The parsed message, or <c>null</c> if the connection closed or the frame
    /// could not be parsed as a valid control message.
    /// </returns>
    public static async Task<ControlMessage?> ReadMessageAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        byte[]? payload = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
        return payload is null ? null : ControlMessage.FromUtf8(payload);
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> completely from the stream.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the buffer was filled; <c>false</c> if end-of-stream was reached
    /// before any bytes of the buffer were read or part-way through (clean disconnect).
    /// </returns>
    private static async Task<bool> ReadExactlyOrNullAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer[read..], cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                return false;
            }
            read += n;
        }
        return true;
    }
}
