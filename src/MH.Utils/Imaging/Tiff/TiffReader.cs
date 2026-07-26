using MH.Utils.IO;
using System;
using System.IO;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffReader : BinarySpanReader {
  private TiffEntryData[]? _ifd0;
  private TiffEntryData[]? _exifIfd;
  private TiffEntryData[]? _gpsIfd;

  public uint Ifd0Offset { get; }

  public TiffReader(byte[] buffer) : base(buffer) {
    if (_buffer.Length < 8)
      throw new InvalidDataException("Invalid TIFF header.");

    if (_buffer[0] == 'I' && _buffer[1] == 'I')
      IsLittleEndian = true;
    else if (_buffer[0] == 'M' && _buffer[1] == 'M')
      IsLittleEndian = false;
    else
      throw new InvalidDataException("Invalid TIFF byte order.");

    if (ReadUInt16(2) != 42)
      throw new InvalidDataException("Invalid TIFF magic.");

    Ifd0Offset = ReadUInt32(4);

    if (Ifd0Offset >= _buffer.Length)
      throw new InvalidDataException("Invalid IFD0 offset.");
  }

  public uint GetNextIfdOffset(uint ifdOffset) {
    ushort count = ReadUInt16(ifdOffset);
    uint offset = ifdOffset + 2 + (uint)(count * 12);
    return ReadUInt32(offset);
  }

  public TiffEntryData[] GetIfd0() =>
    _ifd0 ??= ReadIfd(Ifd0Offset);

  public TiffEntryData[] GetExifIfd() {
    _exifIfd ??= _getIfd(GetIfd0(), ExifTag.ExifIfd);
    return _exifIfd;
  }

  public TiffEntryData[] GetGpsIfd() {
    _gpsIfd ??= _getIfd(GetIfd0(), ExifTag.GpsIfd);
    return _gpsIfd;
  }

  private TiffEntryData[] _getIfd(TiffEntryData[] entries, ExifTag tag) =>
    entries.FindEntry(tag) is { } ifd ? ReadIfd(ifd.ValueOrOffset) : [];

  public TiffEntryData[] ReadIfd(uint offset) {
    ushort count = ReadUInt16(offset);
    offset += 2;

    var entries = new TiffEntryData[count];

    for (int i = 0; i < count; i++) {
      entries[i] = new TiffEntryData(
        offset,
        ReadUInt16(offset),
        ReadUInt16(offset + 2),
        ReadUInt32(offset + 4),
        ReadUInt32(offset + 8));

      offset += 12;
    }

    return entries;
  }

  public ushort GetShortValue(TiffEntryData entry) {
    if (entry.Type != 3)
      throw new InvalidDataException("Entry is not SHORT.");

    if (entry.Count != 1)
      throw new InvalidDataException("Entry is not a single SHORT.");

    return IsLittleEndian
      ? (ushort)(entry.ValueOrOffset & 0xFFFF)
      : (ushort)(entry.ValueOrOffset >> 16);
  }

  public char ReadAsciiChar(uint valueOrOffset, uint count) {
    if (count <= 4)
      return IsLittleEndian
        ? (char)(valueOrOffset & 0xFF)
        : (char)(valueOrOffset >> 24);

    return (char)GetSpan(valueOrOffset, 1)[0];
  }

  public ReadOnlySpan<byte> GetValueSpan(TiffEntryData entry) {
    var type = (TiffType)entry.Type;
    var count = (int)entry.Count;
    int size = type.GetDataLength(count);

    if (type.IsInline(count))
      return _buffer.AsSpan((int)entry.EntryOffset + 8, size);

    return _buffer.AsSpan((int)entry.ValueOrOffset, size);
  }
}