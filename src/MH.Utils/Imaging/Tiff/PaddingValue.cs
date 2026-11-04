using System;
using System.Buffers.Binary;

namespace MH.Utils.Imaging.Tiff;

public sealed class PaddingValue : DataValue {
  // System.Windows.Media.Imaging.BitmapDecoder requires this padding prefix
  // + 4 bytes of padding length field to be present in the padding value.
  private static ReadOnlySpan<byte> _wicPaddingPrefix => [0x1C, 0xEA, 0x00, 0x00];
  private const int _sizeFieldOffset = 4;

  public const int HeaderLength = 12;

  public PaddingValue(uint? originalOffset, byte[] data) : base(originalOffset, data) {
    _wicPaddingPrefix.CopyTo(data);
    _updateSizeField();
  }

  public int Consume(int requested) {
    int available = Math.Max(0, Data.Length - HeaderLength);
    int consumed = Math.Min(Math.Max(0, requested), available);

    if (consumed > 0) _resizeData(Data.Length - consumed);

    return consumed;
  }

  public void Extend(int bytes) {
    if (bytes > 0) _resizeData(checked(Data.Length + bytes));
  }

  public static bool ValidatePaddingChunk(int paddingChunk) {
    if (paddingChunk < HeaderLength)
      throw new ArgumentOutOfRangeException(nameof(paddingChunk),
        $"The padding chunk must be at least {HeaderLength} bytes.");

    return true;
  }

  private void _resizeData(int length) {
    ArgumentOutOfRangeException.ThrowIfLessThan(length, HeaderLength);

    byte[] data = Data;
    Array.Resize(ref data, length);
    Data = data;

    _updateSizeField();
  }

  private void _updateSizeField() {
    if (Data.Length < HeaderLength) return;

    uint paddingLength = checked((uint)(Data.Length - HeaderLength));
    BinaryPrimitives.WriteUInt32LittleEndian(
      Data.AsSpan(_sizeFieldOffset, sizeof(uint)),
      paddingLength);
  }
}