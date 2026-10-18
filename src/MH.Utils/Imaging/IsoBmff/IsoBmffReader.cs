using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffReader(Stream stream) {
  private readonly byte[] _buffer = new byte[16];

  internal readonly Stream _stream = stream;

  public IsoBmffBox? ReadBox(long parentEnd) {
    var offset = _stream.Position;

    if (parentEnd - offset < 8) return null;

    _stream.ReadExactly(_buffer.AsSpan(0, 8));

    var size32 = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
    var type = BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4));

    long headerSize = 8;
    long size;

    if (size32 == 1) {
      _stream.ReadExactly(_buffer.AsSpan(8, 8));

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
    var end = _stream.Length;

    _stream.Position = 0;

    while (_stream.Position < end) {
      if (ReadBox(end) is not { } box)
        break;

      if (box.Type == type)
        return box;

      _stream.Position = box.End;
    }

    return null;
  }

  public IsoBmffBox? FindChild(IsoBmffBox parent, uint type, long childrenOffset = 0) {
    var offset = parent.DataOffset + childrenOffset;
    var end = parent.End;

    if (offset > end)
      throw new InvalidDataException("Invalid ISO BMFF child offset.");

    _stream.Position = offset;

    while (_stream.Position < end) {
      if (ReadBox(end) is not { } box)
        break;

      if (box.Type == type)
        return box;

      _stream.Position = box.End;
    }

    return null;
  }

  public string? ReadDataString(IsoBmffBox item) {
    if (FindChild(item, IsoBmffTypes.Data) is not { } data || data.DataSize < 8)
      return null;

    var length = checked((int)data.DataSize - 8);

    _stream.Position = data.DataOffset + 8;

    var buffer = new byte[length];
    _stream.ReadExactly(buffer);

    return Encoding.UTF8.GetString(buffer);
  }

  public uint ReadUInt32BigEndian() {
    _stream.ReadExactly(_buffer.AsSpan(0, 4));
    return BinaryPrimitives.ReadUInt32BigEndian(_buffer);
  }

  public uint ReadUInt32BigEndian(long position) {
    _stream.Position = position;
    return ReadUInt32BigEndian();
  }

  public (uint, uint) ReadTwoUInt32BigEndian() {
    _stream.ReadExactly(_buffer.AsSpan(0, 8));
    return new (
      BinaryPrimitives.ReadUInt32BigEndian(_buffer),
      BinaryPrimitives.ReadUInt32BigEndian(_buffer.AsSpan(4)));
  }

  public ulong ReadUInt64BigEndian() {
    _stream.ReadExactly(_buffer.AsSpan(0, 8));
    return BinaryPrimitives.ReadUInt64BigEndian(_buffer);
  }

  public ulong ReadUInt64BigEndian(long position) {
    _stream.Position = position;
    return ReadUInt64BigEndian();
  }

  public byte ReadVersion(IsoBmffBox box) {
    _stream.Position = box.DataOffset;
    _stream.ReadExactly(_buffer.AsSpan(0, 4));
    return _buffer[0];
  }

  [Conditional("DEBUG")]
  internal void _dumpRaw(IsoBmffBox box) {
    _stream.Position = box.DataOffset;

    var length = checked((int)box.DataSize);

    if (length > 1024 * 1024)
      throw new InvalidOperationException("Box is too large to dump.");

    Span<byte> buffer = stackalloc byte[length];
    _stream.ReadExactly(buffer);

    Debug.WriteLine(Convert.ToHexString(buffer));
  }

  public long GetBoxChildrenOffset(IsoBmffBox box) {
    if (box.Type != IsoBmffTypes.Meta) return 0;

    _stream.Position = box.DataOffset;
    if (ReadBox(box.End) is { Type: IsoBmffTypes.Hdlr }) return 0;

    return 4;
  }

  [Conditional("DEBUG")]
  internal void _dumpBoxes() {
    _stream.Position = 0;

    _dumpBoxes(0, _stream.Length, 0);
  }

  [Conditional("DEBUG")]
  internal void _dumpChildren(IsoBmffBox parent) {
    _dumpBoxes(parent.DataOffset, parent.End, 0, parent.Type);
  }

  [Conditional("DEBUG")]
  private void _dumpBoxes(long offset, long end, int depth, uint? parentType = null) {
    _stream.Position = offset;

    while (_stream.Position < end) {
      if (ReadBox(end) is not { } box) return;

      Debug.WriteLine(
        $"{new string(' ', depth * 2)}" +
        $"{box.TypeName,-4} " +
        $"0x{box.Type:X8} " +
        $"offset={box.Offset,10} " +
        $"size={box.Size,10} " +
        $"header={box.HeaderSize,2} " +
        $"data={box.DataSize,10}");

      if (_hasChildren(box.Type, parentType)) {
        var childrenOffset = GetBoxChildrenOffset(box) + box.DataOffset;
        _dumpBoxes(childrenOffset, box.End, depth + 1, box.Type);
      }

      _stream.Position = box.End;
    }
  }

  internal List<IsoBmffNode> _readNodes() {
    var nodes = new List<IsoBmffNode>();
    _readNodes(nodes, -1, 0, _stream.Length);
    return nodes;
  }

  private void _readNodes(List<IsoBmffNode> nodes, int parent, long offset, long end) {
    _stream.Position = offset;

    while (_stream.Position < end) {
      if (ReadBox(end) is not { } box)
        break;

      var index = nodes.Count;
      nodes.Add(new IsoBmffNode(box, parent));

      uint? parentType = parent >= 0
        ? nodes[parent].Box.Type
        : null;

      if (_hasChildren(box.Type, parentType)) {
        var childrenOffset = GetBoxChildrenOffset(box);
        var childrenStart = box.DataOffset + childrenOffset;

        if (childrenStart < box.End)
          _readNodes(nodes, index, childrenStart, box.End);
      }

      _stream.Position = box.End;
    }
  }

  private static bool _hasChildren(uint type, uint? parentType) =>
    _isContainer(type) || parentType == IsoBmffTypes.Ilst;

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