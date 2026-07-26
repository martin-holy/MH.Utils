using System;

namespace MH.Utils.Imaging.Tiff;

public sealed class PaddingValue(uint? originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public int Consume(int requested) {
    int consumed = Math.Min(requested, Data.Length);
    if (consumed == 0) return 0;
    _resizeData(Data.Length - consumed);
    return consumed;
  }

  public void Extend(int bytes) {
    if (bytes <= 0) return;
    _resizeData(Data.Length + bytes);
  }

  private void _resizeData(int length) {
    byte[] data = Data;
    Array.Resize(ref data, length);
    Data = data;
  }
}