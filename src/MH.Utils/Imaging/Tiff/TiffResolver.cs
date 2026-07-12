using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public static class TiffResolver {
  public static void Resolve(TiffReader reader, TiffFile file) {
    _resolveIfd(reader, file.Ifd0, false);
    _resolveHoles(reader, file, file.Ifd0);
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

  private static void _resolveHoles(TiffReader reader, TiffFile file, TiffIfd ifd) {
    _insertHoles(reader, file, ifd);

    foreach (var entry in ifd.Entries) {
      if (entry.SubIfd != null)
        _resolveHoles(reader, file, entry.SubIfd);
    }

    if (ifd.NextIfd != null)
      _resolveHoles(reader, file, ifd.NextIfd);
  }

  private static void _insertHoles(TiffReader reader, TiffFile file, TiffIfd ifd) {
    var values = new List<TiffObject>();

    foreach (var entry in ifd.Entries) {
      if (entry.Value is InlineValue)
        continue;

      if (entry.Value != null)
        values.Add(entry.Value);
    }

    values.Sort(static (a, b) => a.OriginalOffset.CompareTo(b.OriginalOffset));

    for (int i = 0; i < values.Count - 1; i++) {
      var current = values[i];
      var next = values[i + 1];

      uint end = current.OriginalOffset + (uint)current.OriginalSize;

      if (next.OriginalOffset <= end)
        continue;

      int size = checked((int)(next.OriginalOffset - end));

      values.Insert(i + 1,
        new Hole(
          end,
          reader.GetSpan(end, size).ToArray()));

      i++;
    }

    foreach (var value in values) {
      if (value is Hole hole) {
        file.Holes.Add(hole);
      }
    }
  }

  private static uint _readUInt32(bool littleEndian, byte[] bytes) {
    ReadOnlySpan<byte> span = bytes;

    return littleEndian
      ? BinaryPrimitives.ReadUInt32LittleEndian(span)
      : BinaryPrimitives.ReadUInt32BigEndian(span);
  }
}