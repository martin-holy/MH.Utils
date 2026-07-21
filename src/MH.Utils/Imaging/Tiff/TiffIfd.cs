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

  public TiffIfd CreateIfd(ExifTag tag) {
    var ifd = new TiffIfd(null, []);
    var entry = new TiffEntry(tag, TiffType.Long, 1) { SubIfd = ifd };
    AddEntry(entry);
    return ifd;
  }

  public TiffEntry? FindEntry(ExifTag tag) {
    foreach (var entry in Entries) {
      if (entry.Tag == (ushort)tag)
        return entry;

      if (entry.SubIfd != null) {
        var found = entry.SubIfd.FindEntry(tag);
        if (found != null)
          return found;
      }
    }

    return NextIfd?.FindEntry(tag);
  }
}