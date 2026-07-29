using MH.Utils.Imaging.Exif;
using System.IO;

namespace MH.Utils.Imaging.Tiff;

public readonly record struct TiffEntryData(uint EntryOffset, ushort Tag, ushort Type, uint Count, uint ValueOrOffset);

public static class TiffEntryDataExtensions {
  public static TiffEntryData? FindEntry(this TiffEntryData[] entries, ExifTag tag) {
    foreach (var entry in entries)
      if (entry.Tag == (ushort)tag)
        return entry;

    return null;
  }

  public static ushort? GetUShort(this TiffEntryData[] entries, ExifTag tag, bool littleEndian) =>
    entries.FindEntry(tag)?.GetUShortValue(littleEndian);

  public static ushort GetUShortValue(this TiffEntryData entry, bool littleEndian) {
    if (entry.Type != (ushort)TiffType.Short)
      throw new InvalidDataException("Entry is not SHORT.");

    if (entry.Count != 1)
      throw new InvalidDataException("Entry is not a single SHORT.");

    return littleEndian
      ? (ushort)(entry.ValueOrOffset & 0xFFFF)
      : (ushort)(entry.ValueOrOffset >> 16);
  }
}