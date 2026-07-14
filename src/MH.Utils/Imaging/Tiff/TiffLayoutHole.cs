using System;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffLayoutHole(uint offset, byte[] data) {
  public uint OriginalOffset { get; } = offset;
  public byte[] Data { get; private set; } = data;

  public int Size => Data.Length;

  public void Consume(int bytes) {
    byte[] data = Data;
    Array.Resize(ref data, data.Length - bytes);
    Data = data;
  }
}