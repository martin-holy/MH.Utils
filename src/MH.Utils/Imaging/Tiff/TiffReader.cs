using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffReader {
  private readonly byte[] _buffer;

  private ExifEntry[]? _ifd0;
  private ExifEntry[]? _exifIfd;
  private ExifEntry[]? _gpsIfd;

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

  // TODO try to use GetNextIfdOffset in GetExifIfd, GetGpsIfd
  // TODO what this should return if there is not other ifd?
  public uint GetNextIfdOffset(uint ifdOffset) {
    ushort count = ReadUInt16(ifdOffset);
    uint offset = ifdOffset + 2 + (uint)(count * 12);
    return ReadUInt32(offset);
  }

  public ExifEntry[] GetIfd0() =>
    _ifd0 ??= ReadIfd(Ifd0Offset);

  public ExifEntry[] GetExifIfd() {
    if (_exifIfd != null) return _exifIfd;

    if (FindEntry(GetIfd0(), ExifTag.ExifIfd) is { } exifIfd)
      _exifIfd = ReadIfd(exifIfd.ValueOrOffset);
    else
      _exifIfd = [];

    return _exifIfd;
  }

  public ExifEntry[] GetGpsIfd() {
    if (_gpsIfd != null) return _gpsIfd;

    if (FindEntry(GetIfd0(), ExifTag.GpsIfd) is { } gpsIfd)
      _gpsIfd = ReadIfd(gpsIfd.ValueOrOffset);
    else
      _gpsIfd = [];

    return _gpsIfd;
  }

  public static ExifEntry? FindEntry(ExifEntry[] entries, ExifTag tag) {
    foreach (var entry in entries)
      if (entry.Tag == (ushort)tag)
        return entry;

    return null;
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

  public ExifEntry[] ReadIfd(uint offset) {
    ushort count = ReadUInt16(offset);
    offset += 2;

    var entries = new ExifEntry[count];

    for (int i = 0; i < count; i++) {
      entries[i] = new ExifEntry(
        offset,
        ReadUInt16(offset),
        ReadUInt16(offset + 2),
        ReadUInt32(offset + 4),
        ReadUInt32(offset + 8));

      offset += 12;
    }

    return entries;
  }

  public ushort GetShortValue(ExifEntry entry) {
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

  public ReadOnlySpan<byte> GetValueSpan(ExifEntry entry) {
    int size = _getValueSize(entry.Type, entry.Count);

    if (IsInline(entry.Type, entry.Count))
      return _buffer.AsSpan((int)entry.EntryOffset + 8, size);

    return _buffer.AsSpan((int)entry.ValueOrOffset, size);
  }

  private static int _getTypeSize(ushort type) =>
    type switch {
      1 => 1, // BYTE
      2 => 1, // ASCII
      3 => 2, // SHORT
      4 => 4, // LONG
      5 => 8, // RATIONAL
      6 => 1, // SBYTE
      7 => 1, // UNDEFINED
      8 => 2, // SSHORT
      9 => 4, // SLONG
      10 => 8, // SRATIONAL
      11 => 4, // FLOAT
      12 => 8, // DOUBLE
      _ => throw new NotSupportedException($"Unsupported TIFF type: {type}")
    };

  private static int _getValueSize(ushort type, uint count) =>
    checked(_getTypeSize(type) * (int)count);

  public static bool IsInline(ushort type, uint count) =>
    _getValueSize(type, count) <= 4;
}