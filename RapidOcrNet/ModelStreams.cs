// Apache-2.0 license

namespace RapidOcrNet;

/// <summary>
/// Reads a caller-supplied model stream into the byte array ONNX Runtime needs.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> has no stream constructor, only a path
/// and a <c>byte[]</c> one, so a stream has to be materialised before a session can be built. That
/// is one copy of the model, and it is unavoidable from here: the runtime offers no way to hand it
/// the buffer without it taking (and keeping) its own parsed copy anyway. What the copy buys is
/// that a model can come from anywhere — an embedded resource, a zip entry, a network response —
/// rather than from a file this process is able to open.
/// </para>
/// <para>
/// A seekable stream is read straight into an exactly sized array, so the model is allocated once
/// and copied once. That is the common case: <see cref="FileStream"/>, the
/// <c>UnmanagedMemoryStream</c> behind an embedded resource and <see cref="MemoryStream"/> all
/// report a length. A stream that cannot seek has none to size from, so it is read through a
/// growable buffer and costs a second copy on the way out; both are supported.
/// </para>
/// <para>
/// The stream is only read, never disposed: passing one in does not hand over ownership of it, and
/// the caller stays free to close it or to rewind and reuse it.
/// </para>
/// </remarks>
internal static class ModelStreams
{
    /// <summary>
    /// Rejects a stream that cannot be read from, naming the argument the caller passed.
    /// </summary>
    public static void EnsureReadable(Stream stream, string paramName)
    {
        ArgumentNullException.ThrowIfNull(stream, paramName);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", paramName);
        }
    }

    /// <summary>
    /// Reads <paramref name="stream"/> to its end and leaves it open, positioned at the end. A
    /// stream that cannot seek is supported; one that cannot be read is not.
    /// </summary>
    /// <exception cref="EndOfStreamException">
    /// The stream is seekable but ended before <see cref="Stream.Length"/> bytes had been read.
    /// </exception>
    public static byte[] ReadAllBytes(Stream stream, string paramName)
    {
        EnsureReadable(stream, paramName);

        if (stream.CanSeek)
        {
            long remaining = stream.Length - stream.Position;

            // Far beyond int.MaxValue there is no array to allocate anyway; fall through to the
            // growing path and let the read decide how much there really is.
            if (remaining is >= 0 and <= int.MaxValue)
            {
                byte[] exact = new byte[remaining];

                // ReadExactly rather than a single Read: a stream is free to return fewer bytes
                // than asked for, and FileStream over a network share does so in practice. It
                // throws if the length the stream reported turns out not to be there, which is
                // the one thing the size could not tell us up front.
                stream.ReadExactly(exact);
                return exact;
            }
        }

        // No length to size from: grow a buffer, then hand back what it holds. Pre-sizing would
        // mean guessing, and a wrong guess is either a re-allocation or a wasted 138 MB.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // GetBuffer is permitted on a MemoryStream this method created. Growth leaves the capacity
        // above the length in nearly every case, and the copy that trims it is the price of not
        // having been able to size the array to begin with.
        byte[] bytes = buffer.GetBuffer();
        return bytes.Length == buffer.Length ? bytes : bytes[..(int)buffer.Length];
    }
}
