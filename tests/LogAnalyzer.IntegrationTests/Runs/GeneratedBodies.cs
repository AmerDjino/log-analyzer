using System.Net;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>Request bodies built in code, for the size-limit tests.</summary>
public static class GeneratedBodies
{
    /// <summary>A UTF-8 log of exactly the given size: lines of 99 characters and a line break, the last line cut short.</summary>
    public static byte[] LogOfSize(int byteCount) =>
        Enumerable.Range(0, byteCount).Select(index => index % 100 == 99 ? (byte)'\n' : (byte)'x').ToArray();
}

/// <summary>
/// A text/plain body sent one byte per chunk, so the chunk framing on the wire is five times the log's bytes.
/// </summary>
public sealed class OneBytePerChunkContent : HttpContent
{
    private readonly byte[] _content;

    public OneBytePerChunkContent(byte[] content)
    {
        _content = content;
        Headers.ContentType = new("text/plain");
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        for (var index = 0; index < _content.Length; index++)
        {
            await stream.WriteAsync(_content.AsMemory(index, 1));
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}

/// <summary>
/// A text/plain body whose length is not known in advance, so the client sends it without Content-Length: chunked,
/// over HTTP/1.1.
/// </summary>
public sealed class UnknownLengthContent : HttpContent
{
    private readonly byte[] _content;

    public UnknownLengthContent(byte[] content)
    {
        _content = content;
        Headers.ContentType = new("text/plain");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(_content).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
