using System;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffLayoutHole(byte[] data) {
  public byte[] Data { get; private set; } = data;

  public int Size => Data.Length;

  public int Consume(int requested) {
    int consumed = Math.Min(requested, Data.Length);

    if (consumed == 0) return 0;

    byte[] data = Data;
    Array.Resize(ref data, data.Length - consumed);
    Data = data;

    return consumed;
  }

  public void Write(TiffWriter writer) =>
    writer.WriteBytes(Data);
}