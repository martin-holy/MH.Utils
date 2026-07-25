using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffReader {
  private readonly byte[] _buffer;

  private TiffEntryData[]? _ifd0;
  private TiffEntryData[]? _exifIfd;
  private TiffEntryData[]? _gpsIfd;

  public bool IsLittleEndian { get; }
  public uint Ifd0Offset { get; }
  public byte[] GetBytes() => _buffer;

  public TiffReader(byte[] buffer) {
    _buffer = buffer;

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

  public double ReadRational(uint offset) {
    uint numerator = ReadUInt32(offset);
    uint denominator = ReadUInt32(offset + 4);

    if (denominator == 0) return 0;

    return (double)numerator / denominator;
  }

  public Span<byte> GetSpan(uint offset, int length) {
    ByteU.CheckBounds(_buffer, offset, length); //TODO do I need the check?
    return _buffer.AsSpan((int)offset, length);
  }

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

  // TODO not used
  public string ReadAscii(uint valueOrOffset, uint count) {
    if (count <= 4) {
      Span<byte> bytes = stackalloc byte[4];

      if (IsLittleEndian)
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, valueOrOffset);
      else
        BinaryPrimitives.WriteUInt32BigEndian(bytes, valueOrOffset);

      return ReadAscii(bytes[..(int)count]);
    }

    return ReadAscii(GetSpan(valueOrOffset, (int)count));
  }

  public static string ReadAscii(ReadOnlySpan<byte> span) =>
    Encoding.ASCII.GetString(span).TrimEnd('\0');

  public string ReadUtf16Le(uint offset, uint count) {
    var span = GetSpan(offset, (int)count);
    return Encoding.Unicode.GetString(span).TrimEnd('\0');
  }

  public ReadOnlySpan<byte> GetValueSpan(TiffEntryData entry) {
    int size = _getValueSize(entry.Type, entry.Count);

    if (IsInline(entry.Type, entry.Count))
      return _buffer.AsSpan((int)entry.EntryOffset + 8, size);

    return _buffer.AsSpan((int)entry.ValueOrOffset, size);
  }

  public static int GetTypeSize(TiffType type) =>
    type switch {
      TiffType.Byte => 1,
      TiffType.Ascii => 1,
      TiffType.Short => 2,
      TiffType.Long => 4,
      TiffType.Rational => 8,
      TiffType.SByte => 1,
      TiffType.Undefined => 1,
      TiffType.SShort => 2,
      TiffType.SLong => 4,
      TiffType.SRational => 8,
      TiffType.Float => 4,
      TiffType.Double => 8,
      _ => throw new NotSupportedException($"Unsupported TIFF type: {type}")
    };

  private static int _getValueSize(ushort type, uint count) =>
    checked(GetTypeSize((TiffType)type) * (int)count);

  public static bool IsInline(ushort type, uint count) =>
    _getValueSize(type, count) <= 4;
}