using System.Buffers.Binary;

namespace ClassCast.Common.Protocol;

/// <summary>
/// Writes length-prefixed frames to a stream using the ClassCast wire format:
/// <c>[ 4-byte big-endian length ][ payload bytes ]</c>. Used for both the
/// control channel (JSON payloads) and the broadcast channel (JPEG payloads).
/// </summary>
public static class FrameWriter
{
    /// <summary>
    /// Asynchronously writes a single length-prefixed frame to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination stream (typically a <c>NetworkStream</c>).</param>
    /// <param name="payload">The raw payload bytes to frame.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the frame has been written.</returns>
    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serialises a <see cref="ControlMessage"/> to UTF-8 JSON and writes it as a
    /// single length-prefixed frame.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <param name="message">The message to serialise and send.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>A task that completes when the framed message has been written.</returns>
    public static Task WriteMessageAsync(Stream stream, ControlMessage message, CancellationToken cancellationToken = default)
        => WriteFrameAsync(stream, message.ToUtf8(), cancellationToken);
}
