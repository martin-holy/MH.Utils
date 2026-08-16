using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Xmp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging.Jpeg;

public class JpegFile : IDisposable {
  private const byte _soi = 0xD8;
  private const byte _eoi = 0xD9;
  private const byte _sos = 0xDA;
  private const byte _app1 = 0xE1;

  private static ReadOnlySpan<byte> _exifHeader => "Exif\0\0"u8;
  private static ReadOnlySpan<byte> _xmpHeader => "http://ns.adobe.com/xap/1.0/\0"u8;
  private static ReadOnlySpan<byte> _xmpExtHeader => "http://ns.adobe.com/xmp/extension/\0"u8;
  private const string _extXmpAttr = "HasExtendedXMP=\"";

  private readonly Stream _stream;
  private readonly BinaryReader _reader;
  private readonly List<JpegSegment> _segments = [];

  private long _scanPosition;
  private bool _scanComplete;

  private ushort? _width;
  private ushort? _height;

  private JpegSegment? _exifSegment;
  private JpegSegment? _xmpSegment;

  private ExifMetadata? _exif;
  private XmpMetadata? _xmp;

  public ushort Width {
    get {
      _ensureSize();
      return _width!.Value;
    }
  }

  public ushort Height {
    get {
      _ensureSize();
      return _height!.Value;
    }
  }

  public ExifMetadata Exif {
    get {
      if (_exif is null) {
        _ensureExif();
        _exif = _readExif(_exifSegment) ?? new ExifMetadata(null);
      }

      return _exif;
    }
  }

  public XmpMetadata Xmp {
    get {
      if (_xmp is null) {
        _ensureXmp();
        _xmp = _readXmp(_xmpSegment) ?? new XmpMetadata(null);
      }

      return _xmp;
    }
  }

  public JpegFile(string filePath)
    : this(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
  }

  public JpegFile(Stream stream) {
    _stream = stream;
    _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    _validateJpeg();

    _scanPosition = _stream.Position;
  }

  public bool Write(string srcPath) {
    var exif = Exif.IsModified ? Exif.ToTiff() : null;
    var xmp = Xmp.Doc.IsModified ? Xmp.ToPacket() : null;

    if (exif == null && xmp == null) return true;

    var writer = new JpegMetadataWriter {
      Exif = exif,
      Xmp = xmp
    };

    var tmpPath = srcPath + ".tmp";

    try {
      using var input = File.OpenRead(srcPath);
      using var output = File.Create(tmpPath);
      writer.Write(input, output);

      File.Delete(srcPath);
      File.Move(tmpPath, srcPath);

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex, srcPath);

      try {
        if (File.Exists(tmpPath))
          File.Delete(tmpPath);
      }
      catch { }

      return false;
    }
  }

  private void _validateJpeg() {
    if (_stream.Length - _stream.Position < 2)
      throw new InvalidDataException("Not a JPEG file.");

    if (_reader.ReadByte() != 0xFF || _reader.ReadByte() != _soi)
      throw new InvalidDataException("Not a JPEG file.");
  }

  private void _ensureSize() {
    if (_width.HasValue) return;

    while (!_scanComplete)
      if (!_readNextSegment())
        break;
  }

  private void _ensureExif() {
    if (_exifSegment is not null || _scanComplete) return;

    while (!_scanComplete && _exifSegment is null)
      _readNextSegment();
  }

  private void _ensureXmp() {
    if (_xmpSegment is not null || _scanComplete) return;

    while (!_scanComplete && _xmpSegment is null)
      _readNextSegment();
  }

  private bool _readNextSegment() {
    _stream.Position = _scanPosition;

    if (_stream.Position >= _stream.Length) {
      _scanComplete = true;

      return false;
    }

    if (_reader.ReadByte() != 0xFF)
      throw new InvalidDataException("Invalid JPEG marker.");

    byte marker;

    do {
      if (_stream.Position >= _stream.Length)
        throw new InvalidDataException("Invalid JPEG marker.");

      marker = _reader.ReadByte();
    } while (marker == 0xFF);

    if (marker == _sos || marker == _eoi) {
      _scanComplete = true;
      _scanPosition = _stream.Position;

      return false;
    }

    long markerPosition = _stream.Position - 2;

    if (!_hasLength(marker)) {
      _segments.Add(new JpegSegment(marker, markerPosition, 2, 0, 0));
      _scanPosition = _stream.Position;

      return true;
    }

    if (_stream.Length - _stream.Position < 2)
      throw new InvalidDataException("Invalid JPEG segment.");

    ushort segmentLength = _readUInt16(_reader);

    if (segmentLength < 2)
      throw new InvalidDataException("Invalid JPEG segment.");

    int payloadLength = segmentLength - 2;
    long payloadPosition = _stream.Position;

    if (payloadPosition + payloadLength > _stream.Length)
      throw new InvalidDataException("JPEG segment exceeds file length.");

    var segment = new JpegSegment(marker, markerPosition, segmentLength + 2, payloadPosition, payloadLength);

    _segments.Add(segment);

    if (_isStartOfFrame(marker) && !_width.HasValue)
      _readSize(segment);

    if (marker == _app1)
      _inspectApp1(segment);

    _stream.Position = payloadPosition + payloadLength;
    _scanPosition = _stream.Position;

    return true;
  }

  private void _readSize(JpegSegment segment) {
    if (segment.PayloadLength < 5)
      throw new InvalidDataException("Invalid JPEG SOF segment.");

    _stream.Position = segment.PayloadPosition;

    _reader.ReadByte(); // precision

    _height = _readUInt16(_reader);
    _width = _readUInt16(_reader);
  }

  private void _inspectApp1(JpegSegment segment) {
    int length = Math.Min(
      segment.PayloadLength,
      Math.Max(_exifHeader.Length, _xmpHeader.Length));

    if (length == 0) return;

    Span<byte> header = stackalloc byte[length];

    _stream.Position = segment.PayloadPosition;
    _stream.ReadExactly(header);

    if (header.StartsWith(_exifHeader)) {
      _exifSegment ??= segment;
      return;
    }

    if (header.StartsWith(_xmpHeader))
      _xmpSegment ??= segment;
  }

  private ExifMetadata? _readExif(JpegSegment? segment) {
    if (segment == null) return null;
    int offset = _exifHeader.Length;
    int length = segment.PayloadLength - offset;

    if (length < 0)
      throw new InvalidDataException("Invalid EXIF segment.");

    _stream.Position = segment.PayloadPosition + offset;

    var data = new byte[length];
    _stream.ReadExactly(data);

    return new ExifMetadata(new TiffReader(data));
  }

  private XmpMetadata? _readXmp(JpegSegment? segment) {
    if (segment == null) return null;

    var mainPayload = _readSegmentPayload(segment);

    var xmlOffset = _xmpHeader.Length;

    if (xmlOffset < mainPayload.Length && mainPayload[xmlOffset] == 0)
      xmlOffset++;

    var mainXml = _tryDecodeXml(mainPayload, xmlOffset, mainPayload.Length - xmlOffset);

    if (mainXml is null) return null;

    if (_findExtendedGuid(mainXml) is not { } extendedGuid)
      return new XmpMetadata(mainXml);

    var chunks = new List<(int Offset, byte[] Data)>();
    int fullLength = 0;

    _readExtendedXmp(extendedGuid, chunks, ref fullLength);

    if (chunks.Count == 0)
      return new XmpMetadata(mainXml);

    var full = new byte[fullLength];

    foreach (var (offset, data) in chunks) {
      if (offset < 0 || offset + data.Length > full.Length)
        continue;

      Buffer.BlockCopy(data, 0, full, offset, data.Length);
    }

    var extendedXml = _tryDecodeXml(full, 0, full.Length);

    return extendedXml is not null
      ? new XmpMetadata(extendedXml)
      : new XmpMetadata(mainXml);
  }

  private void _readExtendedXmp(string guid, List<(int Offset, byte[] Data)> chunks, ref int fullLength) {
    while (!_scanComplete) {
      if (!_readNextSegment())
        break;

      var segment = _segments[^1];

      if (segment.Marker != _app1) continue;

      var payload = _readSegmentPayload(segment);

      if (!payload.AsSpan().StartsWith(_xmpExtHeader)) continue;

      var p = _xmpExtHeader.Length;

      if (payload.Length < p + 32 + 8) continue;

      var segmentGuid = Encoding.ASCII.GetString(payload, p, 32);

      p += 32;

      if (!string.Equals(segmentGuid, guid, StringComparison.Ordinal)) continue;

      int segmentFullLength = _readBigEndianInt32(payload, ref p);
      int offset = _readBigEndianInt32(payload, ref p);

      if (segmentFullLength < 0) continue;

      if (offset < 0 || offset > segmentFullLength) continue;

      var chunk = payload[p..];

      if (fullLength == 0)
        fullLength = segmentFullLength;

      if (segmentFullLength != fullLength) continue;

      chunks.Add((offset, chunk));

      if (_hasCompleteExtendedXmp(chunks, fullLength)) break;
    }
  }

  private static bool _hasCompleteExtendedXmp(List<(int Offset, byte[] Data)> chunks, int fullLength) {
    if (fullLength == 0)
      return false;

    var ranges = new List<(int Start, int End)>(chunks.Count);

    foreach (var (offset, data) in chunks)
      ranges.Add((offset, offset + data.Length));

    ranges.Sort((a, b) => a.Start.CompareTo(b.Start));

    var end = 0;

    foreach (var (start, chunkEnd) in ranges) {
      if (start > end)
        return false;

      if (chunkEnd > end)
        end = chunkEnd;

      if (end >= fullLength)
        return true;
    }

    return false;
  }

  private byte[] _readSegmentPayload(JpegSegment segment) {
    if (segment.Payload is not null)
      return segment.Payload;

    _stream.Position = segment.PayloadPosition;

    var payload = new byte[segment.PayloadLength];
    _stream.ReadExactly(payload);

    segment.Payload = payload;

    return payload;
  }

  private static string? _findExtendedGuid(string xml) {
    int index = xml.IndexOf(_extXmpAttr, StringComparison.Ordinal);

    if (index < 0) return null;

    int start = index + _extXmpAttr.Length;
    int end = xml.IndexOf('"', start);

    return end > start
      ? xml[start..end]
      : null;
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

    try {
      var utf8 = Encoding.UTF8.GetString(buffer, offset, length);

      if (utf8.Contains("<x:xmpmeta", StringComparison.Ordinal) ||
        utf8.Contains("<rdf:RDF", StringComparison.Ordinal) ||
        utf8.Contains("<?xpacket", StringComparison.Ordinal) ||
        utf8.TrimStart().StartsWith('<'))
        return utf8;
    }
    catch { }

    try {
      var unicode = Encoding.Unicode.GetString(buffer, offset, length);

      if (unicode.Contains("<x:xmpmeta", StringComparison.Ordinal) ||
        unicode.Contains("<rdf:RDF", StringComparison.Ordinal) ||
        unicode.Contains("<?xpacket", StringComparison.Ordinal))
        return unicode;
    }
    catch { }

    return null;
  }

  private static int _readBigEndianInt32(byte[] buffer, ref int position) {
    if (position + 4 > buffer.Length)
      throw new InvalidDataException("Invalid XMP extended segment.");

    int value =
      (buffer[position] << 24) |
      (buffer[position + 1] << 16) |
      (buffer[position + 2] << 8) |
      buffer[position + 3];

    position += 4;
    return value;
  }

  private static ushort _readUInt16(BinaryReader reader) {
    byte high = reader.ReadByte();
    byte low = reader.ReadByte();

    return (ushort)((high << 8) | low);
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

  public void Dispose() {
    _reader.Dispose();
    _stream.Dispose();
  }
}