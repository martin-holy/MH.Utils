namespace MH.Utils.Imaging.Tiff;

public readonly record struct TiffEntryData(uint EntryOffset, ushort Tag, ushort Type, uint Count, uint ValueOrOffset);

public static class TiffEntryDataExtensions {
  public static TiffEntryData? FindEntry(this TiffEntryData[] entries, ExifTag tag) {
    foreach (var entry in entries)
      if (entry.Tag == (ushort)tag)
        return entry;

    return null;
  }
}