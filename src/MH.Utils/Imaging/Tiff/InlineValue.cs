using System;

namespace MH.Utils.Imaging.Tiff;

public sealed class InlineValue(uint? originalOffset, byte[] data) : DataValue(originalOffset, data) {
  public override void Write(TiffWriter writer) {
    throw new InvalidOperationException("Inline values are written by TiffEntry.");
  }
}