namespace MH.Utils.Imaging.Tiff;

public static class TiffParser {
  public static TiffFile Parse(TiffReader reader) =>
    new(_parseIfd(reader, reader.Ifd0Offset));

  private static TiffIfd _parseIfd(TiffReader reader, uint ifdOffset) {
    var ifd = new TiffIfd(ifdOffset, []);

    foreach (var entry in reader.ReadIfd(ifdOffset))
      ifd.Entries.Add(_parseEntry(reader, entry));

    var next = reader.GetNextIfdOffset(ifdOffset);

    if (next != 0)
      ifd.NextIfd = _parseIfd(reader, next);

    return ifd;
  }

  private static TiffEntry _parseEntry(TiffReader reader, ExifEntry entry) {
    var result = new TiffEntry(entry.Tag, entry.Type, entry.Count);

    if (_isSubIfd(entry.Tag)) {
      result.SubIfd = _parseIfd(reader, entry.ValueOrOffset);
      return result;
    }

    var data = reader.GetValueSpan(entry).ToArray();

    if (TiffReader.IsInline(entry.Type, entry.Count))
      result.Value = new InlineValue(0, data);
    else
      result.Value = new DataValue(entry.ValueOrOffset, data);

    return result;
  }

  private static bool _isSubIfd(ushort tag) =>
    tag is
      (ushort)ExifTag.ExifIfd or
      (ushort)ExifTag.GpsIfd;/* or
      (ushort)ExifTag.InteropIfd;*/ // TODO add the rest
}