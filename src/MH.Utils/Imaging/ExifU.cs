using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace MH.Utils.Imaging;

public readonly record struct ExifEntry(ushort Tag, ushort Type, uint Count, uint ValueOrOffset);

public enum ExifTag : ushort {
  Orientation = 0x0112,

  ExifIfd = 0x8769,
  GpsIfd = 0x8825,

  UserComment = 0x9286,
  XpComment = 0x9C9C,

  GpsLatitudeRef = 0x0001,
  GpsLatitude = 0x0002,
  GpsLongitudeRef = 0x0003,
  GpsLongitude = 0x0004,
}

public sealed class ExifU {
  private static ReadOnlySpan<byte> _exifHeader => "Exif\0\0"u8;
  private static ReadOnlySpan<byte> _asciiHeader => "ASCII\0\0\0"u8;
  private static ReadOnlySpan<byte> _unicodeHeader => "UNICODE"u8;
  private static ReadOnlySpan<byte> _jisHeader => "JIS\0\0\0\0\0"u8;

  private readonly byte[] _tiff;

  private ExifEntry[]? _ifd0;
  private ExifEntry[]? _exifIfd;
  private ExifEntry[]? _gpsIfd;

  private readonly bool _littleEndian;
  private readonly uint _ifd0Offset;

  private ExifU(byte[] tiff) {
    _tiff = tiff;

    if (_tiff.Length < 8)
      throw new InvalidDataException("Invalid TIFF header.");

    if (_tiff[0] == 'I' && _tiff[1] == 'I')
      _littleEndian = true;
    else if (_tiff[0] == 'M' && _tiff[1] == 'M')
      _littleEndian = false;
    else
      throw new InvalidDataException("Invalid TIFF byte order.");

    if (_readUInt16(2) != 42)
      throw new InvalidDataException("Invalid TIFF magic.");

    _ifd0Offset = _readUInt32(4);

    if (_ifd0Offset >= _tiff.Length)
      throw new InvalidDataException("Invalid IFD0 offset.");
  }

  public static ExifU? ReadFromJpeg(string path) {
    using var fs = File.OpenRead(path);
    return ReadFromJpeg(fs);
  }

  public static ExifU? ReadFromJpeg(Stream stream) {
    using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    if (stream.Length < 4)
      return null;

    if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8)
      return null;

    Span<byte> header = stackalloc byte[_exifHeader.Length];

    while (stream.Position + 4 <= stream.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      if (marker == 0xDA || marker == 0xD9)
        break;

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      if (marker != 0xE1) {
        stream.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      if (payloadLen < _exifHeader.Length) {
        stream.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      stream.ReadExactly(header);

      if (!header.SequenceEqual(_exifHeader)) {
        stream.Seek(payloadLen - header.Length, SeekOrigin.Current);
        continue;
      }

      return new ExifU(br.ReadBytes(payloadLen - header.Length));
    }

    return null;
  }

  public ushort? GetOrientation() {
    if (_findEntry(_getIfd0(), ExifTag.Orientation) is not { } entry) return null;
    return _getShortValue(entry);
  }

  public string? GetComment() =>
    GetXpComment() ?? GetUserComment();

  public string? GetXpComment() {
    if (_findEntry(_getIfd0(), ExifTag.XpComment) is not { Type: 1 } entry) return null;
    return _readUtf16Le(entry.ValueOrOffset, entry.Count);
  }

  public string? GetUserComment() {
    if (_findEntry(_getIfd0(), ExifTag.ExifIfd) is not { } exifIfd)
      return null;

    if (_findEntry(_getExifIfd(exifIfd.ValueOrOffset), ExifTag.UserComment) is not { Type: 7 } comment)
      return null;

    if (comment.Count < 8)
      return string.Empty;

    var span = _getSpan(comment.ValueOrOffset, (int)comment.Count);

    if (span[..8].SequenceEqual(_asciiHeader))
      return Encoding.ASCII.GetString(span[8..]).TrimEnd('\0');

    if (span[..8].SequenceEqual(_unicodeHeader))
      return Encoding.BigEndianUnicode.GetString(span[8..]).TrimEnd('\0');

    if (span[..8].SequenceEqual(_jisHeader))
      return null; // Not implemented.

    return null;
  }

  public bool TryGetLatLong(out double latitude, out double longitude) {
    latitude = 0;
    longitude = 0;

    if (_findEntry(_getIfd0(), ExifTag.GpsIfd) is not { } gpsIfd)
      return false;

    var gps = _getGpsIfd(gpsIfd.ValueOrOffset);

    if (_findEntry(gps, ExifTag.GpsLatitudeRef) is not { } latRef
      || _findEntry(gps, ExifTag.GpsLatitude) is not { Type: 5, Count: 3 } lat
      || _findEntry(gps, ExifTag.GpsLongitudeRef) is not { } lngRef
      || _findEntry(gps, ExifTag.GpsLongitude) is not { Type: 5, Count: 3 } lng)
      return false;

    latitude = _readGpsCoordinate(lat.ValueOrOffset);
    longitude = _readGpsCoordinate(lng.ValueOrOffset);

    if (_readAsciiChar(latRef.ValueOrOffset, latRef.Count) == 'S')
      latitude = -latitude;

    if (_readAsciiChar(lngRef.ValueOrOffset, lngRef.Count) == 'W')
      longitude = -longitude;

    return true;
  }

  private double _readGpsCoordinate(uint offset) {
    double degrees = _readRational(offset);
    double minutes = _readRational(offset + 8);
    double seconds = _readRational(offset + 16);

    return degrees + minutes / 60.0 + seconds / 3600.0;
  }

  private double _readRational(uint offset) {
    uint numerator = _readUInt32(offset);
    uint denominator = _readUInt32(offset + 4);

    if (denominator == 0) return 0;

    return (double)numerator / denominator;
  }

  private ushort _getShortValue(ExifEntry entry) {
    if (entry.Type != 3)
      throw new InvalidDataException("Entry is not SHORT.");

    if (entry.Count != 1)
      throw new InvalidDataException("Entry is not a single SHORT.");

    return _littleEndian
      ? (ushort)(entry.ValueOrOffset & 0xFFFF)
      : (ushort)(entry.ValueOrOffset >> 16);
  }

  private static ExifEntry? _findEntry(ExifEntry[] entries, ExifTag tag) {
    foreach (var entry in entries)
      if (entry.Tag == (ushort)tag)
        return entry;

    return null;
  }

  private ExifEntry[] _getIfd0() =>
    _ifd0 ??= _readIfd(_ifd0Offset);

  private ExifEntry[] _getExifIfd(uint offset) =>
    _exifIfd ??= _readIfd(offset);

  private ExifEntry[] _getGpsIfd(uint offset) =>
    _gpsIfd ??= _readIfd(offset);

  private ExifEntry[] _readIfd(uint offset) {
    ushort count = _readUInt16(offset);
    offset += 2;

    var entries = new ExifEntry[count];

    for (int i = 0; i < count; i++) {
      entries[i] = new ExifEntry(
        _readUInt16(offset),
        _readUInt16(offset + 2),
        _readUInt32(offset + 4),
        _readUInt32(offset + 8));

      offset += 12;
    }

    return entries;
  }

  private char _readAsciiChar(uint valueOrOffset, uint count) {
    if (count <= 4)
      return _littleEndian
        ? (char)(valueOrOffset & 0xFF)
        : (char)(valueOrOffset >> 24);

    return (char)_getSpan(valueOrOffset, 1)[0];
  }

  private string _readAscii(uint valueOrOffset, uint count) {
    if (count <= 4) {
      Span<byte> bytes = stackalloc byte[4];

      if (_littleEndian)
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, valueOrOffset);
      else
        BinaryPrimitives.WriteUInt32BigEndian(bytes, valueOrOffset);

      return _readAscii(bytes[..(int)count]);
    }

    return _readAscii(_getSpan(valueOrOffset, (int)count));
  }

  private static string _readAscii(ReadOnlySpan<byte> span) =>
    Encoding.ASCII.GetString(span).TrimEnd('\0');

  private string _readUtf16Le(uint offset, uint count) {
    var span = _getSpan(offset, (int)count);
    return Encoding.Unicode.GetString(span).TrimEnd('\0');
  }

  private ushort _readUInt16(uint offset) {
    var span = _getSpan(offset, 2);

    return _littleEndian
      ? BinaryPrimitives.ReadUInt16LittleEndian(span)
      : BinaryPrimitives.ReadUInt16BigEndian(span);
  }

  private uint _readUInt32(uint offset) {
    var span = _getSpan(offset, 4);

    return _littleEndian
      ? BinaryPrimitives.ReadUInt32LittleEndian(span)
      : BinaryPrimitives.ReadUInt32BigEndian(span);
  }

  private ReadOnlySpan<byte> _getSpan(uint offset, int length) {
    ByteU.CheckBounds(_tiff, offset, length);
    return _tiff.AsSpan((int)offset, length);
  }
}