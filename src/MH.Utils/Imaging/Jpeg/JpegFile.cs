using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Xmp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Jpeg;

[Flags]
public enum JpegMetadataLoad {
  None = 0,
  Size = 1,
  Exif = 2,
  Xmp = 4,
  All = Size | Exif | Xmp
}

public class JpegFile {
  private const byte _soi = 0xD8;
  private const byte _eoi = 0xD9;
  private const byte _sos = 0xDA;
  private const byte _app1 = 0xE1;

  private static ReadOnlySpan<byte> _exifHeader => "Exif\0\0"u8;
  private static ReadOnlySpan<byte> _xmpHeader => "http://ns.adobe.com/xap/1.0/\0"u8;
  private static ReadOnlySpan<byte> _xmpExtHeader => "http://ns.adobe.com/xmp/extension/\0"u8;
  private const string _extXmpAttr = "HasExtendedXMP=\"";

  private readonly string? _filePath;
  private readonly byte[]? _data;

  private ushort? _width;
  private ushort? _height;

  private ExifMetadata? _exif;
  private XmpMetadata? _xmp;
  private bool _exifRead;
  private bool _xmpRead;

  public ushort Width => _getWidth();
  public ushort Height => _getHeight();
  public ExifMetadata Exif => _getExif();
  public XmpMetadata Xmp => _getXmp();

  public JpegFile(string filePath, JpegMetadataLoad load = JpegMetadataLoad.None) {
    _filePath = filePath;
    using var source = File.OpenRead(filePath);
    _read(source, load);
  }

  public JpegFile(Stream stream, JpegMetadataLoad load = JpegMetadataLoad.None) {
    using var data = new MemoryStream();
    stream.CopyTo(data);

    _data = data.ToArray();

    using var source = new MemoryStream(_data, writable: false);
    _read(source, load);
  }

  private void _read(Stream stream, JpegMetadataLoad load) {
    if (load == JpegMetadataLoad.None) return;

    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    _validateJpeg(stream, reader);

    var needSize = load.HasFlag(JpegMetadataLoad.Size);
    var needExif = load.HasFlag(JpegMetadataLoad.Exif);
    var needXmp = load.HasFlag(JpegMetadataLoad.Xmp);

    while (stream.Position < stream.Length) {
      if (_readSegment(stream, reader) is not { } segment) break;

      if (needSize && !_width.HasValue && _isStartOfFrame(segment.Marker))
        _readSize(stream, reader, segment);

      if (segment.Marker == _app1) {
        if (needExif && _exif == null && segment.StartsWith(_exifHeader, stream))
          _exif = _readExif(stream, segment);

        if (needXmp && _xmp == null && segment.StartsWith(_xmpHeader, stream))
          _xmp = _readXmp(stream, reader, segment);
      }

      stream.Position = segment.PayloadPosition + segment.PayloadLength;

      if (_width.HasValue &&
        (!needExif || _exif != null) &&
        (!needXmp || _xmp != null))
        break;
    }

    if (needExif) _exifRead = true;
    if (needXmp) _xmpRead = true;
  }

  private static void _validateJpeg(Stream stream, BinaryReader reader) {
    if (stream.Length - stream.Position < 2
      || reader.ReadByte() != 0xFF
      || reader.ReadByte() != _soi)
      throw new InvalidDataException("Not a JPEG file.");
  }

  private static JpegSegment? _readSegment(Stream stream, BinaryReader reader) {
    if (stream.Position >= stream.Length)
      return null;

    if (reader.ReadByte() != 0xFF)
      throw new InvalidDataException("Invalid JPEG marker.");

    byte marker;

    do {
      if (stream.Position >= stream.Length)
        throw new InvalidDataException("Invalid JPEG marker.");

      marker = reader.ReadByte();
    } while (marker == 0xFF);

    if (marker == _sos || marker == _eoi)
      return null;

    long markerPosition = stream.Position - 2;

    if (!_hasLength(marker))
      return new JpegSegment(marker, markerPosition, 2, stream.Position, 0);

    if (stream.Length - stream.Position < 2)
      throw new InvalidDataException("Invalid JPEG segment.");

    ushort segmentLength = ByteU.ReadBigEndianUInt16(reader);

    if (segmentLength < 2)
      throw new InvalidDataException("Invalid JPEG segment.");

    int payloadLength = segmentLength - 2;
    long payloadPosition = stream.Position;

    if (stream.Length - payloadPosition < payloadLength)
      throw new InvalidDataException("JPEG segment exceeds file length.");

    var segment = new JpegSegment(marker, markerPosition, segmentLength + 2, payloadPosition, payloadLength);

    stream.Position = payloadPosition + payloadLength;

    return segment;
  }

  private void _readSize(Stream stream, BinaryReader reader, JpegSegment segment) {
    if (segment.PayloadLength < 5)
      throw new InvalidDataException("Invalid JPEG SOF segment.");

    stream.Position = segment.PayloadPosition;
    reader.ReadByte();

    _height = ByteU.ReadBigEndianUInt16(reader);
    _width = ByteU.ReadBigEndianUInt16(reader);
  }

  private static ExifMetadata _readExif(Stream stream, JpegSegment segment) {
    var data = segment.ReadPayloadData(stream, _exifHeader.Length);

    return new ExifMetadata(new TiffReader(data));
  }

  private static XmpMetadata? _readXmp(Stream stream, BinaryReader reader, JpegSegment mainSegment) {
    var mainPayload = mainSegment.ReadPayload(stream);
    var xmlOffset = _xmpHeader.Length;

    if (xmlOffset < mainPayload.Length && mainPayload[xmlOffset] == 0)
      xmlOffset++;

    if (_tryDecodeXml(mainPayload, xmlOffset, mainPayload.Length - xmlOffset) is not { } mainXml)
      return null;

    if (_findExtendedGuid(mainXml) is not { } extendedGuid)
      return new XmpMetadata(mainXml);

    var chunks = new List<(int Offset, byte[] Data)>();
    var fullLength = 0;

    while (true) {
      if (_readSegment(stream, reader) is not { } segment) break;

      if (segment.Marker != _app1) continue;

      var payload = segment.ReadPayload(stream);

      if (!payload.AsSpan().StartsWith(_xmpExtHeader)) continue;

      var p = _xmpExtHeader.Length;

      if (payload.Length < p + 32 + 8) continue;

      var guid = Encoding.ASCII.GetString(payload, p, 32);
      p += 32;

      if (!string.Equals(guid, extendedGuid, StringComparison.Ordinal)) continue;

      var segmentFullLength = _readBigEndianInt32(payload, ref p);
      var offset = _readBigEndianInt32(payload, ref p);

      if (segmentFullLength < 0 || offset < 0 || offset > segmentFullLength) continue;

      if (fullLength == 0)
        fullLength = segmentFullLength;

      if (segmentFullLength != fullLength) continue;

      var chunk = payload[p..];

      if (offset + chunk.Length > fullLength) continue;

      chunks.Add((offset, chunk));

      if (_hasCompleteExtendedXmp(chunks, fullLength)) break;
    }

    if (chunks.Count == 0)
      return new XmpMetadata(mainXml);

    var full = new byte[fullLength];

    foreach (var (offset, data) in chunks)
      Buffer.BlockCopy(data, 0, full, offset, data.Length);

    return _tryDecodeXml(full, 0, full.Length) is { } extendedXml
      ? new XmpMetadata(extendedXml)
      : new XmpMetadata(mainXml);
  }

  private static string? _tryDecodeXml(byte[] buffer, int offset, int length) {
    if (length <= 0) return null;

    if (length >= 3 && buffer[offset] == 0xEF && buffer[offset + 1] == 0xBB && buffer[offset + 2] == 0xBF)
      return Encoding.UTF8.GetString(buffer, offset + 3, length - 3);

    if (length >= 2) {
      if (buffer[offset] == 0xFF && buffer[offset + 1] == 0xFE)
        return Encoding.Unicode.GetString(buffer, offset + 2, length - 2);

      if (buffer[offset] == 0xFE && buffer[offset + 1] == 0xFF)
        return Encoding.BigEndianUnicode.GetString(buffer, offset + 2, length - 2);
    }

    var utf8 = Encoding.UTF8.GetString(buffer, offset, length);
    if (_looksLikeXml(utf8)) return utf8;

    var unicode = Encoding.Unicode.GetString(buffer, offset, length);
    if (_looksLikeXml(unicode)) return unicode;

    return null;
  }

  private static bool _looksLikeXml(string text) =>
    text.Contains("<x:xmpmeta", StringComparison.Ordinal) ||
    text.Contains("<rdf:RDF", StringComparison.Ordinal) ||
    text.Contains("<?xpacket", StringComparison.Ordinal) ||
    text.TrimStart().StartsWith('<');

  private static string? _findExtendedGuid(string xml) {
    var index = xml.IndexOf(_extXmpAttr, StringComparison.Ordinal);

    if (index < 0) return null;

    var start = index + _extXmpAttr.Length;
    var end = xml.IndexOf('"', start);

    return end > start ? xml[start..end] : null;
  }

  private static int _readBigEndianInt32(byte[] buffer, ref int position) {
    if (position + 4 > buffer.Length)
      throw new InvalidDataException("Invalid XMP extended segment.");

    return ByteU.ReadBigEndianInt32(buffer, ref position);
  }

  private static bool _hasCompleteExtendedXmp(List<(int Offset, byte[] Data)> chunks, int fullLength) {
    if (fullLength == 0) return false;

    var ranges = new List<(int Start, int End)>(chunks.Count);

    foreach (var (offset, data) in chunks)
      ranges.Add((offset, offset + data.Length));

    ranges.Sort((a, b) => a.Start.CompareTo(b.Start));

    var end = 0;

    foreach (var (start, chunkEnd) in ranges) {
      if (start > end) return false;
      if (chunkEnd > end) end = chunkEnd;
      if (end >= fullLength) return true;
    }

    return false;
  }

  private static bool _isStartOfFrame(byte marker) =>
    marker is
      0xC0 or 0xC1 or 0xC2 or 0xC3 or
      0xC5 or 0xC6 or 0xC7 or
      0xC9 or 0xCA or 0xCB or
      0xCD or 0xCE or 0xCF;

  private static bool _hasLength(byte marker) =>
    marker switch {
      0xD8 or 0xD9 => false,
      >= 0xD0 and <= 0xD7 => false,
      0x01 => false,
      _ => true
    };

  private ushort _getWidth() {
    if (!_width.HasValue) _readSize();
    return _width!.Value;
  }

  private ushort _getHeight() {
    if (!_height.HasValue) _readSize();
    return _height!.Value;
  }

  private void _readSize() {
    using var stream = _open();
    _read(stream, JpegMetadataLoad.Size);

    if (!_width.HasValue || !_height.HasValue)
      throw new InvalidDataException("JPEG size not found.");
  }

  private ExifMetadata _getExif() {
    if (_exif != null) return _exif;

    if (!_exifRead) {
      using var stream = _open();
      _read(stream, JpegMetadataLoad.Exif);
    }

    return _exif ??= new ExifMetadata(null);
  }

  private XmpMetadata _getXmp() {
    if (_xmp != null) return _xmp;

    if (!_xmpRead) {
      using var stream = _open();
      _read(stream, JpegMetadataLoad.Xmp);
    }

    return _xmp ??= new XmpMetadata(null);
  }

  private Stream _open() {
    if (_data is not null)
      return new MemoryStream(_data, writable: false);

    if (_filePath is null)
      throw new InvalidOperationException("The JPEG file has no source path.");

    return File.OpenRead(_filePath);
  }

  public static bool RemoveMetadata(string srcPath) =>
    Write(srcPath, new JpegMetadataWriter(), true);

  public bool Write(string srcPath) {
    if (_createWriterIfModified() is not { } writer) return true;

    return Write(srcPath, writer);
  }

  public bool Write(Stream source, string destPath) {
    if (_createWriterIfModified() is not { } writer) return true;

    return Write(source, destPath, writer);
  }

  private JpegMetadataWriter? _createWriterIfModified() {
    var exif = _exif?.IsModified == true ? _exif.ToTiff() : null;
    var xmp = _xmp?.Doc?.IsModified == true ? _xmp.ToPacket() : null;

    if (exif == null && xmp == null) return null;

    return new JpegMetadataWriter { Exif = exif, Xmp = xmp };
  }

  public static bool Write(string srcPath, JpegMetadataWriter writer, bool removeMetadata = false) =>
    _write(File.OpenRead(srcPath), srcPath, writer, removeMetadata, true);

  public static bool Write(Stream input, string destPath, JpegMetadataWriter writer, bool removeMetadata = false) =>
    _write(input, destPath, writer, removeMetadata, false);

  private static bool _write(Stream input, string destPath, JpegMetadataWriter writer, bool removeMetadata, bool canCloseInput) {
    var tmpPath = destPath + ".tmp";

    try {
      using (var output = File.Create(tmpPath))
        writer.Write(input, output, removeMetadata);

      if (canCloseInput) input.Close();

      File.Move(tmpPath, destPath, true);

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex, destPath);

      try {
        if (File.Exists(tmpPath))
          File.Delete(tmpPath);
      }
      catch { }

      return false;
    }
  }

  // --- for tests ---
  internal bool SizeIsNull() => _width == null || _height == null;
  internal bool XmpIsNull() => _xmp == null;
  internal bool ExifIsNull() => _exif == null;
}