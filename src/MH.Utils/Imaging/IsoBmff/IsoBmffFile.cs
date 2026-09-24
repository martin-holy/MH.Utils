using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MH.Utils.Imaging.IsoBmff;

public sealed class IsoBmffFile {
  private readonly Stream _stream;
  internal readonly IsoBmffReader _reader;
  internal readonly IsoBmffMetadata _metadata;

  public int Width { get; }
  public int Height { get; }
  public int Orientation { get; }
  public double? FrameRate { get; }
  public TimeSpan? Duration { get; }

  public IsoBmffFile(Stream stream) {
    _stream = stream;
    _reader = new IsoBmffReader(_stream);
    _metadata = new IsoBmffMetadata(_stream, _reader);

    var moov = _findMoov() ?? throw new InvalidDataException("ISO BMFF moov box not found.");
    var trak = _findVideoTrack(moov) ?? throw new InvalidDataException("Video track not found.");
    var tkhd = _reader.FindChild(trak, IsoBmffTypes.Tkhd) ?? throw new InvalidDataException("Video track has no tkhd box.");

    var (width, height) = _readDimensions(tkhd);
    Width = width;
    Height = height;

    Orientation = _readOrientation(tkhd);

    if (_reader.FindChild(trak, IsoBmffTypes.Mdia) is not { } mdia) return;
    if (_reader.FindChild(mdia, IsoBmffTypes.Mdhd) is not { } mdhd) return;
    if (_reader.FindChild(mdia, IsoBmffTypes.Minf) is not { } minf) return;
    if (_reader.FindChild(minf, IsoBmffTypes.Stbl) is not { } stbl) return;
    if (_reader.FindChild(stbl, IsoBmffTypes.Stts) is not { } stts) return;

    var (timescale, duration) = _readTiming(mdhd);

    Duration = duration;
    FrameRate = _readFrameRate(stts, timescale);
  }

  internal IsoBmffBox? _findMoov() {
    var end = _stream.Length;

    _stream.Position = 0;

    while (_stream.Position < end) {
      if (_reader.ReadBox(end) is not { } box)
        break;

      if (box.Type == IsoBmffTypes.Moov)
        return box;

      _stream.Position = box.Offset + box.Size;
    }

    return null;
  }

  internal IsoBmffBox? _findVideoTrack(IsoBmffBox moov) {
    _stream.Position = moov.DataOffset;
    var end = moov.Offset + moov.Size;

    while (_stream.Position < end) {
      if (_reader.ReadBox(end) is not { } box)
        return null;

      if (box.Type == IsoBmffTypes.Trak &&
          _reader.FindChild(box, IsoBmffTypes.Mdia) is { } mdia &&
          _reader.FindChild(mdia, IsoBmffTypes.Hdlr) is { } hdlr &&
          _reader.ReadUInt32BigEndian(hdlr.DataOffset + 8) == IsoBmffTypes.Vide)
        return box;

      _stream.Position = box.Offset + box.Size;
    }

    return null;
  }

  private (int Width, int Height) _readDimensions(IsoBmffBox tkhd) {
    var version = _reader.ReadVersion(tkhd);
    if (version > 1) throw new InvalidDataException($"Unsupported tkhd version: {version}");

    var widthOffset = version == 0 ? 76 : 88;
    _stream.Position = tkhd.DataOffset + widthOffset;
    var (width, height) = _reader.ReadTwoUInt32BigEndian();

    if (width == 0 || height == 0)
      throw new InvalidDataException("Invalid video dimensions.");

    return ((int)(width >> 16), (int)(height >> 16));
  }

  private int _readOrientation(IsoBmffBox tkhd) {
    var version = _reader.ReadVersion(tkhd);
    if (version > 1) throw new InvalidDataException($"Unsupported tkhd version: {version}");

    var matrixOffset = version == 0 ? 40 : 52;
    Span<byte> buffer = stackalloc byte[36];

    _stream.Position = tkhd.DataOffset + matrixOffset;
    _stream.ReadExactly(buffer);

    var a = BinaryPrimitives.ReadInt32BigEndian(buffer);
    var b = BinaryPrimitives.ReadInt32BigEndian(buffer[4..]);
    var c = BinaryPrimitives.ReadInt32BigEndian(buffer[12..]);
    var d = BinaryPrimitives.ReadInt32BigEndian(buffer[16..]);

    if (a == 0 && b > 0 && c < 0 && d == 0) return 90;
    if (a == 0 && b < 0 && c > 0 && d == 0) return 270;
    if (a < 0 && b == 0 && c == 0 && d < 0) return 180;

    return 0;
  }

  private (uint Timescale, TimeSpan? Duration) _readTiming(IsoBmffBox mdhd) {
    var version = _reader.ReadVersion(mdhd);
    if (version > 1) throw new InvalidDataException($"Unsupported mdhd version: {version}");

    var timescale = _reader.ReadUInt32BigEndian(mdhd.DataOffset + (version == 0 ? 12 : 20));

    if (timescale == 0) return (timescale, null);

    var duration = _readDuration(mdhd, version, timescale);

    return (timescale, duration);
  }

  private TimeSpan? _readDuration(IsoBmffBox mdhd, byte version, uint timescale) {
    if (timescale == 0) return null;

    var durationOffset = version == 0 ? 16 : 24;

    _stream.Position = mdhd.DataOffset + durationOffset;

    var duration = version == 0
      ? _reader.ReadUInt32BigEndian()
      : _reader.ReadUInt64BigEndian();

    return TimeSpan.FromSeconds((double)duration / timescale);
  }

  private double? _readFrameRate(IsoBmffBox stts, uint timescale) {
    var version = _reader.ReadVersion(stts);
    if (version != 0) throw new InvalidDataException($"Unsupported stts version: {version}");

    var entryCount = _reader.ReadUInt32BigEndian();
    if (entryCount == 0 || timescale == 0) return null;

    ulong sampleCount = 0;
    ulong duration = 0;

    for (uint i = 0; i < entryCount; i++) {
      var (count, delta) = _reader.ReadTwoUInt32BigEndian();
      sampleCount += count;
      duration += (ulong)count * delta;
    }

    if (sampleCount == 0 || duration == 0) return null;

    return (double)sampleCount * timescale / duration;
  }

  [Conditional("DEBUG")]
  internal void DumpBoxes() {
    _stream.Position = 0;

    _dumpBoxes(0, _stream.Length, 0);
  }

  [Conditional("DEBUG")]
  internal void DumpChildren(IsoBmffBox parent) {
    _dumpBoxes(parent.DataOffset, parent.Offset + parent.Size, 0);
  }

  [Conditional("DEBUG")]
  private void _dumpBoxes(long offset, long end, int depth) {
    _stream.Position = offset;

    while (_stream.Position < end) {
      if (_reader.ReadBox(end) is not { } box) return;

      Debug.WriteLine(
        $"{new string(' ', depth * 2)}" +
        $"{IsoBmffTypes.GetTypeName(box.Type),-4} " +
        $"0x{box.Type:X8} " +
        $"offset={box.Offset,10} " +
        $"size={box.Size,10} " +
        $"header={box.HeaderSize,2} " +
        $"data={box.DataSize,10}");

      if (_isContainer(box.Type)) {
        var childrenOffset = _metadata.GetChildrenOffset(box) + box.DataOffset;
        _dumpBoxes(childrenOffset, box.Offset + box.Size, depth + 1);
      }

      _stream.Position = box.Offset + box.Size;
    }
  }

  internal List<IsoBmffBoxNode> ReadBoxes() {
    var boxes = new List<IsoBmffBoxNode>();
    _readBoxes(boxes, -1, 0, _stream.Length);
    return boxes;
  }

  private void _readBoxes(List<IsoBmffBoxNode> boxes, int parent, long offset, long end) {
    _stream.Position = offset;

    while (_stream.Position < end) {
      if (_reader.ReadBox(end) is not { } box)
        break;

      var index = boxes.Count;
      boxes.Add(new IsoBmffBoxNode(box, parent));

      if (_isContainer(box.Type)) {
        var childrenOffset = _metadata.GetChildrenOffset(box);
        var childrenStart = box.DataOffset + childrenOffset;

        if (childrenStart < box.End)
          _readBoxes(boxes, index, childrenStart, box.End);
      }

      _stream.Position = box.End;
    }
  }

  internal static bool _isContainer(uint type) {
    return type is
      IsoBmffTypes.Moov or
      IsoBmffTypes.Trak or
      IsoBmffTypes.Mdia or
      IsoBmffTypes.Minf or
      IsoBmffTypes.Stbl or
      IsoBmffTypes.Udta or
      IsoBmffTypes.Meta or
      IsoBmffTypes.Ilst or
      IsoBmffTypes.Edts or
      IsoBmffTypes.Dinf or
      IsoBmffTypes.Dref or
      IsoBmffTypes.Mvex or
      IsoBmffTypes.Tref or
      IsoBmffTypes.Sinf or
      IsoBmffTypes.Schi;
  }
}