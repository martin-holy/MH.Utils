using System;
using System.Buffers.Binary;

namespace MH.Utils.Imaging.Tiff;

public static class TiffResolver {
  public static void Resolve(TiffReader reader, TiffFile file) {
    _resolveIfd(reader, file.Ifd0, false);
  }

  private static void _resolveIfd(TiffReader reader, TiffIfd ifd, bool isIfd1) {
    TiffEntry? offsetEntry = null;
    TiffEntry? lengthEntry = null;

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _resolveIfd(reader, entry.SubIfd, false);

      switch ((ExifTag)entry.Tag) {
        case ExifTag.ThumbnailOffset:
          offsetEntry = entry;
          break;

        case ExifTag.ThumbnailLength:
          lengthEntry = entry;
          break;

        case ExifTag.Padding:
          if (entry.Value is DataValue padding)
            entry.Value = new PaddingValue(padding.OriginalOffset, padding.Data);
          break;
      }
    }

    if (offsetEntry != null && lengthEntry != null && isIfd1)
      _resolveJpeg(reader, offsetEntry, lengthEntry);

    if (ifd.NextIfd != null)
      _resolveIfd(reader, ifd.NextIfd, true);
  }

  private static void _resolveJpeg(TiffReader reader, TiffEntry offsetEntry, TiffEntry lengthEntry) {
    if (offsetEntry.Value is not InlineValue offsetValue) return;
    if (lengthEntry.Value is not InlineValue lengthValue) return;

    uint offset = _readUInt32(reader.IsLittleEndian, offsetValue.Data);
    uint length = _readUInt32(reader.IsLittleEndian, lengthValue.Data);

    offsetEntry.Value = new JpegValue(offset, reader.GetSpan(offset, checked((int)length)).ToArray());
  }

  private static uint _readUInt32(bool littleEndian, byte[] bytes) {
    ReadOnlySpan<byte> span = bytes;

    return littleEndian
      ? BinaryPrimitives.ReadUInt32LittleEndian(span)
      : BinaryPrimitives.ReadUInt32BigEndian(span);
  }
}