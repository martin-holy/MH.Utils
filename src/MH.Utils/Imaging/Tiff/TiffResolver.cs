using MH.Utils.Imaging.Exif;

namespace MH.Utils.Imaging.Tiff;

public static class TiffResolver {
  public static void Resolve(TiffReader reader, TiffFile file) {
    _resolveIfd(reader, file, file.Ifd0, false);
  }

  private static void _resolveIfd(TiffReader reader, TiffFile file, TiffIfd ifd, bool isIfd1) {
    TiffEntry? offsetEntry = null;
    TiffEntry? lengthEntry = null;

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _resolveIfd(reader, file, entry.SubIfd, false);

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

        case ExifTag.ExifIfd:
          file.ExifIfd = entry.SubIfd;
          break;
      }
    }

    if (offsetEntry != null && lengthEntry != null && isIfd1)
      _resolveJpeg(reader, offsetEntry, lengthEntry);

    if (ifd.NextIfd != null)
      _resolveIfd(reader, file, ifd.NextIfd, true);
  }

  private static void _resolveJpeg(TiffReader reader, TiffEntry offsetEntry, TiffEntry lengthEntry) {
    if (offsetEntry.Value is not DataValue offsetValue) return;
    if (lengthEntry.Value is not DataValue lengthValue) return;

    uint offset = reader.ReadUInt32(offsetValue.Data);
    uint length = reader.ReadUInt32(lengthValue.Data);

    offsetEntry.Value = new JpegValue(offset, reader.GetSpan(offset, checked((int)length)).ToArray());
  }
}