using MH.Utils.Primitives;
using System;
using System.Buffers.Binary;

namespace MH.Utils.IO;

public ref struct BinarySpanWriter {
  private readonly Span<byte> _buffer;

  public bool IsLittleEndian { get; }
  public int Position { get; private set; }

  public BinarySpanWriter(Span<byte> buffer, bool littleEndian = false) {
    _buffer = buffer;
    IsLittleEndian = littleEndian;
  }

  public void WriteBytes(ReadOnlySpan<byte> bytes) {
    bytes.CopyTo(_buffer[Position..]);
    Position += bytes.Length;
  }

  public void WriteUInt16(ushort value) {
    Span<byte> span = _buffer.Slice(Position, 2);

    if (IsLittleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(span, value);
    else
      BinaryPrimitives.WriteUInt16BigEndian(span, value);

    Position += 2;
  }

  public void WriteUInt32(uint value) {
    Span<byte> span = _buffer.Slice(Position, 4);

    if (IsLittleEndian)
      BinaryPrimitives.WriteUInt32LittleEndian(span, value);
    else
      BinaryPrimitives.WriteUInt32BigEndian(span, value);

    Position += 4;
  }

  public static byte[] GetBytes(Rational[] values, bool littleEndian) {
    byte[] data = new byte[values.Length * 8];
    var writer = new BinarySpanWriter(data, littleEndian);

    foreach (var value in values) {
      writer.WriteUInt32(value.Numerator);
      writer.WriteUInt32(value.Denominator);
    }

    return data;
  }
}