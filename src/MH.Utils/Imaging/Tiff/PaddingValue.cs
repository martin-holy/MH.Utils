using System;

namespace MH.Utils.Imaging.Tiff;

public sealed class PaddingValue(uint? originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public int Consume(int requested) {
    int consumed = Math.Min(requested, Data.Length);

    if (consumed == 0) return 0;

    byte[] data = Data;
    Array.Resize(ref data, data.Length - consumed);
    Data = data;

    return consumed;
  }
}