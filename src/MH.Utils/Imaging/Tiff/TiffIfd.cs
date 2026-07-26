using System;
using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffIfd(uint? originalOffset, List<TiffEntry> entries) : TiffObject(originalOffset) {
  private readonly int _originalEntryCount = entries.Count;

  public List<TiffEntry> Entries { get; } = entries;
  public TiffIfd? NextIfd { get; set; }

  public override int OriginalSize => 2 + _originalEntryCount * 12 + 4;
  public override int CurrentSize => 2 + Entries.Count * 12 + 4;

  public override void Write(TiffWriter writer) {
    WriteOffset = (uint)writer.Position;

    writer.WriteUInt16((ushort)Entries.Count);

    foreach (var entry in Entries)
      entry.Write(writer);

    if (NextIfd == null)
      writer.WriteUInt32(0);
    else
      writer.WriteReference(NextIfd);
  }

  public void AddEntry(TiffEntry entry) {
    Entries.Add(entry);
    Entries.Sort(static (a, b) => a.Tag.CompareTo(b.Tag));
  }

  public bool RemoveEntry(ExifTag tag) {
    if (FindEntry(tag) is { } entry)
      return Entries.Remove(entry);

    return false;
  }

  internal TiffIfd CreateSubIfd(ExifTag tag) {
    var ifd = new TiffIfd(null, []);
    var entry = new TiffEntry(tag, TiffType.Long, 1) { SubIfd = ifd };
    AddEntry(entry);
    return ifd;
  }

  public TiffEntry? FindEntry(ExifTag tag) {
    foreach (var entry in Entries)
      if (entry.Tag == (ushort)tag)
        return entry;

    return null;
  }

  public void SetEntry(ExifTag tag, TiffType type, byte[] data, int count = 0) {
    count = count != 0 ? count : type.GetCount(data.Length);

    if (FindEntry(tag) is not { } entry) {
      entry = new TiffEntry(tag, type, count) {
        Value = new DataValue(null, data)
      };

      AddEntry(entry);
      return;
    }

    if (entry.Value is not DataValue value)
      throw new InvalidOperationException(
        $"Tag {tag} is expected to contain binary data.");

    entry.Count = (uint)count;
    value.Data = data;
  }
}