using MH.Utils.Primitives;
using System;
using System.Buffers.Binary;
using System.Text;

namespace MH.Utils.IO;

public class BinarySpanReader(byte[] buffer) {
  protected readonly byte[] _buffer = buffer;

  public bool IsLittleEndian { get; protected set; }

  public ReadOnlyMemory<byte> Buffer => _buffer;

  public ReadOnlySpan<byte> GetSpan(uint offset, int length) {
    ByteU.CheckBounds(_buffer, offset, length);
    return _buffer.AsSpan((int)offset, length);
  }

  public ushort ReadUInt16(uint offset) {
    var span = GetSpan(offset, 2);

    return IsLittleEndian
      ? BinaryPrimitives.ReadUInt16LittleEndian(span)
      : BinaryPrimitives.ReadUInt16BigEndian(span);
  }

  public uint ReadUInt32(uint offset) {
    var span = GetSpan(offset, 4);

    return IsLittleEndian
      ? BinaryPrimitives.ReadUInt32LittleEndian(span)
      : BinaryPrimitives.ReadUInt32BigEndian(span);
  }

  public uint ReadUInt32(byte[] bytes) {
    ReadOnlySpan<byte> span = bytes;

    return IsLittleEndian
      ? BinaryPrimitives.ReadUInt32LittleEndian(span)
      : BinaryPrimitives.ReadUInt32BigEndian(span);
  }

  public Rational ReadRational(uint offset) =>
    new(ReadUInt32(offset), ReadUInt32(offset + 4));

  public string ReadUtf16(ReadOnlySpan<byte> span) =>
    IsLittleEndian
      ? Encoding.Unicode.GetString(span)
      : Encoding.BigEndianUnicode.GetString(span);
}