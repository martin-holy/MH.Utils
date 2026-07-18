using System;

namespace MH.Utils.Imaging.Tiff;

public abstract class TiffObject(uint? originalOffset) : ITiffWritable {
  public uint? OriginalOffset { get; } = originalOffset;
  public uint WriteOffset { get; set; }

  public virtual int OriginalSize => 0;
  public virtual int CurrentSize => OriginalSize;

  public abstract void Write(TiffWriter writer);
}

public class DataValue(uint? originalOffset, byte[] data) : TiffObject(originalOffset) {
  public byte[] Data { get; set; } = data;

  public override int OriginalSize { get; } = originalOffset != null ? data.Length : 0;
  public override int CurrentSize => Data.Length;

  public override void Write(TiffWriter writer) {
    WriteOffset = (uint)writer.Position;
    writer.WriteBytes(Data);
  }
}

public sealed class InlineValue(uint? originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public override void Write(TiffWriter writer) {
    throw new InvalidOperationException("Inline values are written by TiffEntry.");
  }
}

public sealed class JpegValue(uint originalOffset, byte[] data) : DataValue(originalOffset, data) { }

public sealed class PaddingValue(uint? originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public void Consume(int bytes) {
    byte[] data = Data;
    Array.Resize(ref data, data.Length - bytes);
    Data = data;
  }
}