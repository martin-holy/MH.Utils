using System;
using System.Buffers.Binary;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffReader(Stream stream) {
  private readonly byte[] _buffer = new byte[16];

  public IsoBmffBox? FindMoov() {
    var end = stream.Length;

    stream.Position = 0;

    while (stream.Position < end) {
      if (_readBox(end) is not { } box)
        break;

      if (box.Type == IsoBmffTypes.Moov)
        return box;

      stream.Position = box.Offset + box.Size;
    }

    return null;
  }

  private IsoBmffBox? _readBox(long parentEnd) {
    var offset = stream.Position;

    if (parentEnd - offset < 8) return null;

    _readExactly(_buffer.AsSpan(0, 8));

    var size32 = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    var type = BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4));

    long headerSize = 8;
    long size;

    if (size32 == 1) {
      _readExactly(_buffer.AsSpan(8, 8));

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

  private void _readExactly(Span<byte> buffer) {
    while (!buffer.IsEmpty) {
      var read = stream.Read(buffer);

      if (read == 0)
        throw new EndOfStreamException();

      buffer = buffer[read..];
    }
  }

  public IsoBmffBox? FindChild(IsoBmffBox parent, uint type) {
    stream.Position = parent.DataOffset;

    var end = parent.Offset + parent.Size;

    while (stream.Position < end) {
      if (_readBox(end) is not { } box)
        return null;

      if (box.Type == type)
        return box;

      stream.Position = box.Offset + box.Size;
    }

    return null;
  }

  public IsoBmffBox? FindVideoTrack(IsoBmffBox moov) {
    stream.Position = moov.DataOffset;
    var end = moov.Offset + moov.Size;

    while (stream.Position < end) {
      if (_readBox(end) is not { } box)
        return null;

      if (box.Type == IsoBmffTypes.Trak &&
          FindChild(box, IsoBmffTypes.Mdia) is { } mdia &&
          FindChild(mdia, IsoBmffTypes.Hdlr) is { } hdlr &&
          _isVideoHandler(hdlr))
        return box;

      stream.Position = box.Offset + box.Size;
    }

    return null;
  }

  private bool _isVideoHandler(IsoBmffBox box) {
    stream.Position = box.DataOffset + 8;
    _readExactly(_buffer.AsSpan(0, 4));

    return BinaryPrimitives.ReadUInt32BigEndian(_buffer) == IsoBmffTypes.Vide;
  }

  public (int Width, int Height) ReadDimensions(IsoBmffBox tkhd) {
    var version = _getVersion(tkhd);
    if (version > 1) throw new InvalidDataException($"Unsupported tkhd version: {version}");

    var widthOffset = version == 0 ? 76 : 88;

    stream.Position = tkhd.DataOffset + widthOffset;
    _readExactly(_buffer.AsSpan(0, 8));

    var width = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    var height = BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4));

    if (width == 0 || height == 0)
      throw new InvalidDataException("Invalid video dimensions.");

    return ((int)(width >> 16), (int)(height >> 16));
  }

  public int ReadOrientation(IsoBmffBox tkhd) {
    var version = _getVersion(tkhd);
    if (version > 1) throw new InvalidDataException($"Unsupported tkhd version: {version}");

    var matrixOffset = version == 0 ? 40 : 52;
    Span<byte> buffer = stackalloc byte[36];

    stream.Position = tkhd.DataOffset + matrixOffset;
    _readExactly(buffer);

    var a = BinaryPrimitives.ReadInt32BigEndian(buffer);
    var b = BinaryPrimitives.ReadInt32BigEndian(buffer[4..]);
    var c = BinaryPrimitives.ReadInt32BigEndian(buffer[12..]);
    var d = BinaryPrimitives.ReadInt32BigEndian(buffer[16..]);

    if (a == 0 && b > 0 && c < 0 && d == 0) return 90;
    if (a == 0 && b < 0 && c > 0 && d == 0) return 270;
    if (a < 0 && b == 0 && c == 0 && d < 0) return 180;

    return 0;
  }

  public (uint Timescale, TimeSpan? Duration) ReadTiming(IsoBmffBox mdhd) {
    var version = _getVersion(mdhd);
    if (version > 1) throw new InvalidDataException($"Unsupported mdhd version: {version}");

    var timescale = _readTimescale(mdhd, version);

    if (timescale == 0) return (timescale, null);

    var duration = _readDuration(mdhd, version, timescale);

    return (timescale, duration);
  }

  private uint _readTimescale(IsoBmffBox mdhd, byte version) {
    var timescaleOffset = version == 0 ? 12 : 20;

    stream.Position = mdhd.DataOffset + timescaleOffset;
    _readExactly(_buffer.AsSpan(0, 4));

    return BinaryPrimitives.ReadUInt32BigEndian(_buffer);
  }

  private TimeSpan? _readDuration(IsoBmffBox mdhd, byte version, uint timescale) {
    if (timescale == 0) return null;

    var durationOffset = version == 0 ? 16 : 24;

    stream.Position = mdhd.DataOffset + durationOffset;

    ulong duration;

    if (version == 0) {
      _readExactly(_buffer.AsSpan(0, 4));
      duration = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    }
    else {
      _readExactly(_buffer.AsSpan(0, 8));
      duration = BinaryPrimitives.ReadUInt64BigEndian(_buffer);
    }

    return TimeSpan.FromSeconds((double)duration / timescale);
  }

  public double? ReadFrameRate(IsoBmffBox stts, uint timescale) {
    var version = _getVersion(stts);
    if (version != 0) throw new InvalidDataException($"Unsupported stts version: {version}");

    _readExactly(_buffer.AsSpan(0, 4));
    var entryCount = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    if (entryCount == 0 || timescale == 0) return null;

    ulong sampleCount = 0;
    ulong duration = 0;

    for (uint i = 0; i < entryCount; i++) {
      _readExactly(_buffer.AsSpan(0, 8));

      var count = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
      var delta = BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4));

      sampleCount += count;
      duration += (ulong)count * delta;
    }

    if (sampleCount == 0 || duration == 0) return null;

    return (double)sampleCount * timescale / duration;
  }

  private byte _getVersion(IsoBmffBox box) {
    stream.Position = box.DataOffset;
    _readExactly(_buffer.AsSpan(0, 4));
    return _buffer[0];
  }
}