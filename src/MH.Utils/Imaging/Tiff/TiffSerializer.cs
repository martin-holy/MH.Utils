using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffSerializer {
  public static byte[] Serialize(TiffFile file, bool littleEndian) {
    using var ms = new MemoryStream();

    var writer = new TiffWriter(ms, littleEndian);
    writer.WriteHeader();

    _writeIfd(writer, file.Ifd0);

    writer.FlushDeferred();

    return ms.ToArray();
  }

  private static void _writeIfd(TiffWriter writer, TiffIfd ifd) {
    ifd.Write(writer);

    foreach (var entry in ifd.Entries) {
      if (entry.Value is InlineValue || entry.Value == null)
        continue;

      _writeObject(writer, entry.Value);
    }

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

    if (obj.HoleAfter != null)
      writer.WriteZeros(obj.HoleAfter.Size);
  }
}