namespace MH.Utils.Imaging.Tiff;

public sealed class TiffEntry(ushort tag, ushort type, uint count) {
  public ushort Tag { get; } = tag;
  public ushort Type { get; } = type;
  public uint Count { get; set; } = count;
  public TiffObject? Value { get; set; }
  public TiffIfd? SubIfd { get; set; }

  public TiffEntry(ExifTag tag, TiffType type, int count) : this((ushort)tag, (ushort)type, (uint)count) { }

  public void Write(TiffWriter writer) {
    writer.WriteUInt16(Tag);
    writer.WriteUInt16(Type);
    writer.WriteUInt32(Count);

    if (SubIfd != null)
      writer.WriteReference(SubIfd);
    else if (Value is InlineValue inline)
      writer.WriteInlineValue(inline.Data);
    else
      writer.WriteReference(Value!);
  }
}