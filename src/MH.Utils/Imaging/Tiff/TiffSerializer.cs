using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffSerializer {
  public static byte[] Serialize(TiffFile file, bool littleEndian) {
    _resetWriteOffsets(file.Ifd0);

    using var ms = new MemoryStream();

    var writer = new TiffWriter(ms, littleEndian);
    writer.WriteHeader();

    _writeIfd(writer, file.Ifd0);

    writer.FlushDeferred();

    return ms.ToArray();
  }

  private static void _resetWriteOffsets(TiffIfd ifd) {
    ifd.WriteOffset = 0;

    foreach (var entry in ifd.Entries) {
      if (entry.Value is { } value)
        value.WriteOffset = 0;

      if (entry.SubIfd != null)
        _resetWriteOffsets(entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _resetWriteOffsets(ifd.NextIfd);
  }

  private static void _writeIfd(TiffWriter writer, TiffIfd ifd) {
    ifd.Write(writer);

    foreach (var entry in ifd.Entries)
      if (entry.Value is { } value && !entry.IsInline)
        _writeObject(writer, value);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd == null)
        continue;

      _writeIfd(writer, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _writeIfd(writer, ifd.NextIfd);
  }

  private static void _writeObject(TiffWriter writer, TiffObject obj) {
    obj.Write(writer);
    obj.HoleAfter?.Write(writer);
  }
}