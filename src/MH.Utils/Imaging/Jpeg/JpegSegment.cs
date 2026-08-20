using System;
using System.IO;

namespace MH.Utils.Imaging.Jpeg;

internal readonly record struct JpegSegment(
  byte Marker,
  long Position,
  int Length,
  long PayloadPosition,
  int PayloadLength);

internal static class JpegSegmentExtensions {
  internal static byte[] ReadPayload(this JpegSegment segment, Stream stream) {
    var payload = new byte[segment.PayloadLength];
    stream.Position = segment.PayloadPosition;
    stream.ReadExactly(payload);

    return payload;
  }

  internal static byte[] ReadPayloadData(this JpegSegment segment, Stream stream, int headerLength) {
    var length = segment.PayloadLength - headerLength;

    if (length < 0)
      throw new InvalidDataException("Invalid segment.");

    var data = new byte[length];
    stream.Position = segment.PayloadPosition + headerLength;
    stream.ReadExactly(data);

    return data;
  }

  internal static bool StartsWith(this JpegSegment segment, ReadOnlySpan<byte> prefix, Stream stream) {
    if (segment.PayloadLength < prefix.Length) return false;

    Span<byte> buffer = stackalloc byte[prefix.Length];
    stream.Position = segment.PayloadPosition;
    stream.ReadExactly(buffer);

    return buffer.SequenceEqual(prefix);
  }
}