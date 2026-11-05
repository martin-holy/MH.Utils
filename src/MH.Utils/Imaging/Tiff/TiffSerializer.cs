using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffSerializer {
  public static byte[] Serialize(TiffFile file, bool littleEndian) {
    _resetWriteOffsets(file.Ifd0);

    using var ms = new MemoryStream();

    var writer = new TiffWriter(ms, littleEndian);
    writer.WriteHeader();

    _writeIfdTables(writer, file.Ifd0);
    _writeOutOfLineValues(writer, file.Ifd0);
    _writePaddingValues(writer, file.Ifd0);

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

  private static void _writeIfdTables(TiffWriter writer, TiffIfd ifd) {
    // Write the IFD and its hole before proceeding to another IFD table.
    _writeObject(writer, ifd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _writeIfdTables(writer, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _writeIfdTables(writer, ifd.NextIfd);
  }

  private static void _writeOutOfLineValues(TiffWriter writer, TiffIfd ifd) {
    // Write descendant values before this IFD's values. This places IFD0
    // out-of-line values such as XPComment after its child IFD values.
    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _writeOutOfLineValues(writer, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _writeOutOfLineValues(writer, ifd.NextIfd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd == null
        && entry.Value is { } value
        && value is not PaddingValue
        && !entry.IsInline)
        _writeObject(writer, value);
    }
  }

  private static void _writePaddingValues(TiffWriter writer, TiffIfd ifd) {
    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _writePaddingValues(writer, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _writePaddingValues(writer, ifd.NextIfd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd == null
        && entry.Value is PaddingValue value
        && !entry.IsInline)
        _writeObject(writer, value);
    }
  }

  private static void _writeObject(TiffWriter writer, TiffObject obj) {
    obj.Write(writer);
    obj.HoleAfter?.Write(writer);
  }
}