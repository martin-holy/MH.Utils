using System;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Jpeg;

internal static class JpegSegmentLayout {
  private const byte _app1 = 0xE1;

  internal readonly record struct Layout(JpegSegment? Exif, JpegSegment? Xmp, bool HasExtendedXmp);

  public static Layout Parse(Stream stream) {
    if (!stream.CanRead || !stream.CanSeek)
      throw new ArgumentException("The stream must support reading and seeking.", nameof(stream));

    var originalPosition = stream.Position;

    try {
      stream.Position = 0;

      using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
      JpegFile.ValidateJpeg(stream, reader);

      JpegSegment? exif = null;
      JpegSegment? xmp = null;
      var hasExtendedXmp = false;

      while (stream.Position < stream.Length) {
        if (JpegFile.ReadSegment(stream, reader) is not { } segment)
          break;

        if (segment.Marker == _app1) {
          if (exif is null && segment.StartsWith(JpegFile.ExifHeader, stream))
            exif = segment;
          else if (xmp is null && segment.StartsWith(JpegFile.XmpHeader, stream))
            xmp = segment;

          if (segment.StartsWith(JpegFile.XmpExtHeader, stream))
            hasExtendedXmp = true;
        }

        stream.Position = segment.PayloadPosition + segment.PayloadLength;
      }

      return new Layout(exif, xmp, hasExtendedXmp);
    }
    finally {
      stream.Position = originalPosition;
    }
  }
}