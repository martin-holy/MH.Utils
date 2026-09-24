using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffReader(Stream stream) {
  public readonly byte[] _buffer = new byte[16];

  public IsoBmffBox? ReadBox(long parentEnd) {
    var offset = stream.Position;

    if (parentEnd - offset < 8) return null;

    stream.ReadExactly(_buffer.AsSpan(0, 8));

    var size32 = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    var type = BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4));

    long headerSize = 8;
    long size;

    if (size32 == 1) {
      stream.ReadExactly(_buffer.AsSpan(8, 8));

      var size64 = BinaryPrimitives.ReadUInt64BigEndian(_buffer.AsSpan(8, 8));
      if (size64 > long.MaxValue)
        throw new InvalidDataException("Invalid ISO BMFF box size.");

      size = (long)size64;
      headerSize = 16;
    }
    else if (size32 == 0) {
      size = parentEnd - offset;
    }
    else {
      size = size32;
    }

    if (size < headerSize)
      throw new InvalidDataException("Invalid ISO BMFF box size.");

    if (size > parentEnd - offset)
      throw new InvalidDataException("ISO BMFF box extends beyond its parent.");

    return new IsoBmffBox(offset, headerSize, size, type);
  }

  public IsoBmffBox? Find(uint type) {
    var end = stream.Length;

    stream.Position = 0;

    while (stream.Position < end) {
      if (ReadBox(end) is not { } box)
        break;

      if (box.Type == type)
        return box;

      stream.Position = box.End;
    }

    return null;
  }

  public IsoBmffBox? FindChild(IsoBmffBox parent, uint type, long childrenOffset = 0) {
    var offset = parent.DataOffset + childrenOffset;
    var end = parent.End;

    if (offset > end)
      throw new InvalidDataException("Invalid ISO BMFF child offset.");

    stream.Position = offset;

    while (stream.Position < end) {
      if (ReadBox(end) is not { } box)
        break;

      if (box.Type == type)
        return box;

      stream.Position = box.End;
    }

    return null;
  }

  public uint ReadUInt32BigEndian() {
    stream.ReadExactly(_buffer.AsSpan(0, 4));
    return BinaryPrimitives.ReadUInt32BigEndian(_buffer);
  }

  public uint ReadUInt32BigEndian(long position) {
    stream.Position = position;
    return ReadUInt32BigEndian();
  }

  public (uint, uint) ReadTwoUInt32BigEndian() {
    stream.ReadExactly(_buffer.AsSpan(0, 8));
    return new (
      BinaryPrimitives.ReadUInt32BigEndian(_buffer),
      BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4)));
  }

  public ulong ReadUInt64BigEndian() {
    stream.ReadExactly(_buffer.AsSpan(0, 8));
    return BinaryPrimitives.ReadUInt64BigEndian(_buffer);
  }

  public ulong ReadUInt64BigEndian(long position) {
    stream.Position = position;
    return ReadUInt64BigEndian();
  }

  public byte ReadVersion(IsoBmffBox box) {
    stream.Position = box.DataOffset;
    stream.ReadExactly(_buffer.AsSpan(0, 4));
    return _buffer[0];
  }

  public void DumpRaw(IsoBmffBox box) {
    stream.Position = box.DataOffset;

    var length = checked((int)box.DataSize);

    if (length > 1024 * 1024)
      throw new InvalidOperationException("Box is too large to dump.");

    Span<byte> buffer = stackalloc byte[length];
    stream.ReadExactly(buffer);

    Debug.WriteLine(Convert.ToHexString(buffer));
  }
}