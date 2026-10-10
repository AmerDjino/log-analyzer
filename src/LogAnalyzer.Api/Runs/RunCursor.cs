using System.Buffers.Binary;
using System.Buffers.Text;
using LogAnalyzer.Infrastructure.Runs;

namespace LogAnalyzer.Api.Runs;

/// <summary>
/// The opaque page cursor: base64url of the last listed run's submission time (UTC ticks, 8 bytes) and id (16 bytes).
/// It is not signed: the ownership filter still applies, so a made-up cursor only moves the page within the caller's
/// own runs.
/// </summary>
internal static class RunCursor
{
    private const int ByteLength = 24;

    // Base64url without padding.
    private const int TextLength = 32;

    public static string Encode(Run run)
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        BinaryPrimitives.WriteInt64BigEndian(bytes, run.SubmittedAt.UtcTicks);
        run.Id.TryWriteBytes(bytes[8..]);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string text, out RunPosition position)
    {
        position = default;
        Span<byte> bytes = stackalloc byte[ByteLength];
        if (text.Length != TextLength
            || !Base64Url.TryDecodeFromChars(text, bytes, out var written)
            || written != ByteLength)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        position = new RunPosition(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[8..]));
        return true;
    }
}
