using System;
using System.Collections.Generic;

namespace MH.Utils.Imaging.Tiff;

public abstract class TiffObject(uint originalOffset) : ITiffWritable {
  public uint OriginalOffset { get; } = originalOffset;
  public uint WriteOffset { get; set; }

  public virtual int OriginalSize => 0;
  public virtual int CurrentSize => OriginalSize;
  public virtual int ShrinkableBytes => 0;

  public abstract void Write(TiffWriter writer);
}

public sealed class TiffIfd(uint originalOffset, List<TiffEntry> entries) : TiffObject(originalOffset) {
  public List<TiffEntry> Entries { get; } = entries;
  public TiffIfd? NextIfd { get; set; }

  public override int OriginalSize =>
    2 + Entries.Count * 12 + 4;

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

public sealed class InlineValue(uint originalOffset, byte[] data) : TiffObject(originalOffset) {
  public byte[] Data { get; } = data;

  public override void Write(TiffWriter writer) {
    throw new InvalidOperationException("Inline values are written by TiffEntry.");
  }
}

public class DataValue(uint originalOffset, byte[] data) : TiffObject(originalOffset) {
  public byte[] Data { get; set; } = data;

  public override int OriginalSize { get; } = data.Length;
  public override int CurrentSize => Data.Length;

  public override void Write(TiffWriter writer) {
    WriteOffset = (uint)writer.Position;
    writer.WriteBytes(Data);
  }
}

internal sealed class JpegValue(uint originalOffset, byte[] data) : DataValue(originalOffset, data) { }

public abstract class ShrinkableValue(uint originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public override int ShrinkableBytes => Data.Length;

  public void Consume(int bytes) {
    byte[] data = Data;
    Array.Resize(ref data, data.Length - bytes);
    Data = data;
  }
}

public sealed class Hole(uint originalOffset, byte[] data) : ShrinkableValue(originalOffset, data) { }

public sealed class PaddingValue(uint originalOffset, byte[] data) : ShrinkableValue(originalOffset, data) { }