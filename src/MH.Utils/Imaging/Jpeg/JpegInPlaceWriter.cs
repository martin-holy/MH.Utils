using MH.Utils.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.Imaging.Jpeg;

internal static class JpegInPlaceWriter {
  public static JpegWritePlan CreateWritePlan(string filePath, JpegMetadataWriter writer) {
    using var stream = File.OpenRead(filePath);
    return CreateWritePlan(stream, writer);
  }

  public static JpegWritePlan CreateWritePlan(Stream source, JpegMetadataWriter writer) {
    if (!source.CanRead || !source.CanSeek)
      throw new ArgumentException("The source must support reading and seeking.", nameof(source));

    var layout = JpegSegmentLayout.Parse(source);
    var writes = new List<BinaryWrite>(2);

    var removeExif = (writer.RemoveOptions & RemoveMetadataOptions.Exif) != 0;
    var removeXmp = (writer.RemoveOptions & RemoveMetadataOptions.Xmp) != 0;

    if (removeExif) {
      if (layout.Exif is not null)
        return _fullRewrite(writer);
    }
    else if (writer.Exif is not null) {
      if (layout.Exif is not { } exif)
        return _fullRewrite(writer);

      var replacement = _createPayload(JpegFile.ExifHeader, writer.Exif);

      if (!_tryAddReplacement(exif, replacement, writes))
        return _fullRewrite(writer);
    }

    if (removeXmp) {
      if (layout.Xmp is not null || layout.HasExtendedXmp)
        return _fullRewrite(writer);
    }
    else if (writer.Xmp is not null) {
      if (layout.Xmp is not { } xmp || layout.HasExtendedXmp)
        return _fullRewrite(writer);

      var replacement = _createPayload(JpegFile.XmpHeader, writer.Xmp);

      if (!_tryAddReplacement(xmp, replacement, writes))
        return _fullRewrite(writer);
    }

    return new JpegWritePlan(writer, false, writes);
  }

  private static JpegWritePlan _fullRewrite(JpegMetadataWriter writer) =>
    new(writer, true);

  private static byte[] _createPayload(ReadOnlySpan<byte> header, byte[] metadata) {
    var result = new byte[header.Length + metadata.Length];

    header.CopyTo(result);
    metadata.CopyTo(result, header.Length);

    return result;
  }

  private static bool _tryAddReplacement(JpegSegment original, byte[] replacement, List<BinaryWrite> writes) {
    if (replacement.Length != original.PayloadLength)
      return false;

    writes.Add(new BinaryWrite(original.PayloadPosition, replacement));
    return true;
  }
}