using System.IO;

namespace MH.Utils;

public static class ByteU {
  public static int ReadBigEndianInt32(byte[] buffer, ref int offset) {
    int v =
      (buffer[offset] << 24) |
      (buffer[offset + 1] << 16) |
      (buffer[offset + 2] << 8) |
      buffer[offset + 3];

    offset += 4;
    return v;
  }

  public static ushort ReadBigEndianUInt16(BinaryReader reader) {
    var b0 = reader.ReadByte();
    var b1 = reader.ReadByte();
    return (ushort)((b0 << 8) | b1);
  }

  public static ushort ReadBigEndianUInt16(Stream stream) {
    int b0 = stream.ReadByte();
    int b1 = stream.ReadByte();
    if (b0 < 0 || b1 < 0) throw new EndOfStreamException();
    return (ushort)((b0 << 8) | b1);
  }

  public static void WriteBigEndianUInt16(Stream stream, ushort value) {
    stream.WriteByte((byte)((value >> 8) & 0xFF));
    stream.WriteByte((byte)(value & 0xFF));
  }

  public static void WriteBigEndianUInt32(Stream stream, uint value) {
    stream.WriteByte((byte)(value >> 24));
    stream.WriteByte((byte)(value >> 16));
    stream.WriteByte((byte)(value >> 8));
    stream.WriteByte((byte)value);
  }
}