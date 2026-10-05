using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

internal class IsoBmffMetadata {
  private readonly Stream _stream;
  private readonly IsoBmffReader _reader;
  private readonly IsoBmffBox _moov;
  private List<IsoBmffBoxNode>? _editedBoxes;

  private bool _itemListRead;
  private bool _keysRead;
  private IsoBmffItemList? _itemList;
  private IsoBmffKeys? _keys;

  public int Width { get; }
  public int Height { get; }
  public int Orientation { get; }
  public double? FrameRate { get; }
  public TimeSpan? Duration { get; }

  public IsoBmffMetadata(Stream stream, IsoBmffReader reader) {
    _stream = stream;
    _reader = reader;

    _moov = _reader.Find(IsoBmffTypes.Moov) ?? throw new InvalidDataException("ISO BMFF moov box not found.");
    var trak = _findVideoTrack(_moov) ?? throw new InvalidDataException("Video track not found.");
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

  internal IsoBmffItemList? _getItemList() {
    if (_itemList != null) return _itemList;
    if (!_itemListRead) {
      _itemList = IsoBmffItemList.Find(_reader, _moov);
      _itemListRead = true;
    }

    return _itemList;
  }

  internal IsoBmffKeys? _getKeys() {
    if (_keys != null) return _keys;
    if (!_keysRead) {
      _keys = IsoBmffKeys.Find(_reader, _moov);
      _keysRead = true;
    }

    return _keys;
  }

  public string? GetItemListKeywords() =>
    _getItemList()?.GetKeywords();

  public string? GetKeysKeywords() =>
    _getKeys()?.GetKeywords();

  public string[]? GetKeywords() {
    var keywords = GetItemListKeywords() ?? GetKeysKeywords();
    return keywords?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
  }

  public void SetKeywords(string? value) {
    var itemList = _getOrCreateItemList();
    itemList.SetKeywords(value, _getEditedBoxes());
  }

  private IsoBmffBox? _findVideoTrack(IsoBmffBox moov) {
    _stream.Position = moov.DataOffset;
    var end = moov.End;

    while (_stream.Position < end) {
      if (_reader.ReadBox(end) is not { } box)
        return null;

      if (box.Type == IsoBmffTypes.Trak &&
          _reader.FindChild(box, IsoBmffTypes.Mdia) is { } mdia &&
          _reader.FindChild(mdia, IsoBmffTypes.Hdlr) is { } hdlr &&
          _reader.ReadUInt32BigEndian(hdlr.DataOffset + 8) == IsoBmffTypes.Vide)
        return box;

      _stream.Position = box.End;
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

  public int _readOrientation(IsoBmffBox tkhd) {
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

  internal List<IsoBmffBoxNode> _getEditedBoxes() {
    if (_editedBoxes is not null)
      return _editedBoxes;

    // TODO is this making copy on purpose?
    _editedBoxes = _reader._readBoxes()
      .Select(x => new IsoBmffBoxNode(x.Box, x.Parent))
      .ToList();

    return _editedBoxes;
  }

  internal IsoBmffItemList _getOrCreateItemList() {
    if (_getItemList() is { } itemList) return itemList;

    var editedBoxes = _getEditedBoxes();

    var moovIndex = editedBoxes.FindIndex(IsoBmffTypes.Moov);

    if (moovIndex < 0)
      throw new InvalidOperationException("The moov box was not found.");

    var udtaIndex = editedBoxes.FindIndex(IsoBmffTypes.Udta, moovIndex);

    if (udtaIndex < 0)
      udtaIndex = _addBox(IsoBmffTypes.Udta, 8, moovIndex, editedBoxes);

    var metaIndex = editedBoxes.FindIndex(IsoBmffTypes.Meta, udtaIndex);

    if (metaIndex < 0)
      metaIndex = _addBox(IsoBmffTypes.Meta, 12, udtaIndex, editedBoxes);

    var ilstIndex = editedBoxes.FindIndex(IsoBmffTypes.Ilst, metaIndex);

    if (ilstIndex < 0)
      ilstIndex = _addBox(IsoBmffTypes.Ilst, 8, metaIndex, editedBoxes);

    var ilst = editedBoxes[ilstIndex].Box;

    return _itemList = new IsoBmffItemList(_reader, ilst);
  }

  private static int _addBox(uint type, long size, int parentIndex, List<IsoBmffBoxNode> editedBoxes) {
    var index = editedBoxes.InsertBox(new IsoBmffBox(-1, 8, size, type), parentIndex);

    editedBoxes.UpdateParentSizes(parentIndex, size);

    return index;
  }
}