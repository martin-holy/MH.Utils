using System;
using System.Buffers.Binary;
using System.IO;

namespace MH.Utils.IO;

public class BinaryStreamWriter(Stream stream, bool littleEndian = false) {
  protected readonly Stream _stream = stream;

  public bool IsLittleEndian { get; } = littleEndian;

  public long Position {
    get => _stream.Position;
    set => _stream.Position = value;
  }

  public void PatchUInt32(uint position, uint value) {
    long current = Position;
    Position = position;
    WriteUInt32(value);
    Position = current;
  }

  public void WriteByte(byte value) =>
    _stream.WriteByte(value);

  public void WriteBytes(ReadOnlySpan<byte> bytes) =>
    _stream.Write(bytes);

  public void WriteUInt16(ushort value) {
    Span<byte> buffer = stackalloc byte[2];

    if (IsLittleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
    else
      BinaryPrimitives.WriteUInt16BigEndian(buffer, value);

    _stream.Write(buffer);
  }

  public void WriteUInt32(uint value) {
    Span<byte> buffer = stackalloc byte[4];

    if (IsLittleEndian)
      BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
    else
      BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

    _stream.Write(buffer);
  }
}