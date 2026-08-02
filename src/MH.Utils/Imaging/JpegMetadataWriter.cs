using System;
using System.IO;
using System.Text;

namespace MH.Utils.Imaging;

public sealed class JpegMetadataWriter {
  public static ReadOnlySpan<byte> XmpHeader => "http://ns.adobe.com/xap/1.0/\0"u8;
  public static ReadOnlySpan<byte> XmpExtHeader => "http://ns.adobe.com/xmp/extension/\0"u8;
  public const int App1MaxPayload = 65533;
  public const int ExtChunkDataMax = 65000;
  public const string ExtendedXmpXml =
    @"<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
          <rdf:Description xmlns:xmpNote=""http://ns.adobe.com/xmp/note/"" xmpNote:HasExtendedXMP=""{0}""/>
        </rdf:RDF>
      </x:xmpmeta>";

  private static readonly int _normalCapacity = App1MaxPayload - XmpHeader.Length;
  private enum App1Type { Unknown, Exif, Xmp, ExtendedXmp }
  private const string _extXmpAttr = "HasExtendedXMP=\"";

  private bool _exifHandled;
  private bool _xmpHandled;
  private bool _metadataWritten;

  public byte[]? Exif { get; set; }
  public byte[]? Xmp { get; set; }

  public bool Write(string srcPath) {
    //var tmpPath = srcPath + ".tmp";
    var tmpPath = srcPath + "_output.jpg"; //TODO just for test

    try {
      using var input = File.OpenRead(srcPath);
      using var output = File.Create(tmpPath);
      Write(input, output);

      // TODO removed for testing
      //File.Delete(srcPath);
      //File.Move(tmpPath, srcPath);

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

  public void Write(Stream input, Stream output) {
    using var br = new BinaryReader(input, Encoding.ASCII, true);

    // SOI
    ByteU.CopyBytes(input, output, 2);

    while (input.Position + 4 <= input.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      // Start Of Scan
      if (marker == 0xDA) {
        _writePendingMetadata(output);

        output.WriteByte(0xFF);
        output.WriteByte(marker);

        input.CopyTo(output);
        return;
      }

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      // ---------- APP1 ----------
      if (marker == 0xE1 && _tryProcessApp1(input, output, payloadLen))
        continue;

      // ---------- First non-APP ----------
      if (!_metadataWritten && !_isAppSegment(marker))
        _writePendingMetadata(output);

      // ---------- Copy segment ----------
      output.WriteByte(0xFF);
      output.WriteByte(marker);
      ByteU.WriteBigEndianUInt16(output, segLen);
      ByteU.CopyBytes(input, output, payloadLen);
    }

    throw new InvalidDataException("Unexpected end of JPEG.");
  }

  private static bool _isAppSegment(byte marker) =>
    marker >= 0xE0 && marker <= 0xEF;

  private void _writePendingMetadata(Stream stream) {
    if (_metadataWritten) return;

    if (Exif != null && !_exifHandled)
      _writeExif(stream);

    if (Xmp != null && !_xmpHandled)
      _writeXmp(stream);

    _metadataWritten = true;
  }

  private bool _tryProcessApp1(Stream input, Stream output, int payloadLen) {
    switch (_getApp1Type(input, payloadLen)) {
      case App1Type.Exif:
        _processExif(input, output, payloadLen);
        return true;

      case App1Type.Xmp:
        _processXmp(input, output, payloadLen);
        return true;

      case App1Type.ExtendedXmp:
        _processExtendedXmp(input, output, payloadLen);
        return true;

      default:
        return false;
    }
  }

  private static App1Type _getApp1Type(Stream stream, int payloadLength) {
    long position = stream.Position;

    try {
      if (payloadLength >= ExifU.ExifHeader.Length && _startsWith(stream, ExifU.ExifHeader))
        return App1Type.Exif;

      stream.Position = position;

      if (payloadLength >= XmpHeader.Length && _startsWith(stream, XmpHeader))
        return App1Type.Xmp;

      stream.Position = position;

      if (payloadLength >= XmpExtHeader.Length && _startsWith(stream, XmpExtHeader))
        return App1Type.ExtendedXmp;

      return App1Type.Unknown;
    }
    finally {
      stream.Position = position;
    }
  }

  private static bool _startsWith(Stream stream, ReadOnlySpan<byte> prefix) {
    Span<byte> buffer = stackalloc byte[prefix.Length];
    stream.ReadExactly(buffer);
    return buffer.SequenceEqual(prefix);
  }

  private void _processExif(Stream input, Stream output, int payloadLen) {
    if (Exif == null) {
      _copySegment(input, output, 0xE1, (ushort)(payloadLen + 2));
      _exifHandled = true;
      return;
    }

    input.Seek(payloadLen, SeekOrigin.Current);
    _writeExif(output);
    _exifHandled = true;
  }

  private void _processXmp(Stream input, Stream output, int payloadLength) {
    if (Xmp == null) {
      _copySegment(input, output, 0xE1, (ushort)(payloadLength + 2));
      _xmpHandled = true;
      return;
    }

    input.Seek(payloadLength, SeekOrigin.Current);
    _writeXmp(output);
    _xmpHandled = true;
  }

  private void _processExtendedXmp(Stream input, Stream output, int payloadLength) {
    if (Xmp == null) {
      _copySegment(input, output, 0xE1, (ushort)(payloadLength + 2));
      return;
    }

    input.Seek(payloadLength, SeekOrigin.Current);
  }

  private static void _copySegment(Stream input, Stream output, byte marker, ushort segLen) {
    output.WriteByte(0xFF);
    output.WriteByte(marker);
    ByteU.WriteBigEndianUInt16(output, segLen);
    ByteU.CopyBytes(input, output, segLen - 2);
  }

  private static void _writeSegmentHeader(Stream stream, byte marker, ushort segmentLength) {
    stream.WriteByte(0xFF);
    stream.WriteByte(marker);
    ByteU.WriteBigEndianUInt16(stream, segmentLength);
  }

  private static void _writeApp1(Stream stream, ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload) {
    const int MaxAppPayload = 65533;

    int payloadLength = header.Length + payload.Length;

    if (payloadLength > MaxAppPayload)
      throw new InvalidOperationException(
        "APP1 payload is too large.");

    _writeSegmentHeader(stream, 0xE1, (ushort)(payloadLength + 2));

    stream.Write(header);
    stream.Write(payload);
  }

  private void _writeExif(Stream stream) {
    if (Exif != null)
      _writeApp1(stream, ExifU.ExifHeader, Exif);
  }

  private void _writeXmp(Stream stream) {
    if (Xmp == null) return;

    if (Xmp.Length <= _normalCapacity)
      _writeApp1(stream, XmpHeader, Xmp);
    else
      _writeExtendedXmp(stream, Xmp);

    _xmpHandled = true;
  }

  private static void _writeExtendedXmp(Stream stream, byte[] xmlBytes) {
    var guid = Guid.NewGuid().ToString("N");
    var guidBytes = Encoding.ASCII.GetBytes(guid);
    var mainBytes = Encoding.UTF8.GetBytes(string.Format(ExtendedXmpXml, guid));

    _writeApp1(stream, XmpHeader, mainBytes);

    int offset = 0;
    while (offset < xmlBytes.Length) {
      int chunkLength = Math.Min(ExtChunkDataMax, xmlBytes.Length - offset);
      _writeExtendedChunk(stream, guidBytes, xmlBytes.Length, offset, xmlBytes, offset, chunkLength);
      offset += chunkLength;
    }
  }

  private static void _writeExtendedChunk(Stream stream, byte[] guid, int fullLength, int offset, byte[] data, int dataOffset, int dataLength) {
    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    int payloadLen =
      XmpExtHeader.Length +
      32 + // GUID
      4 +  // full length
      4 +  // offset
      dataLength;

    ByteU.WriteBigEndianUInt16(stream, (ushort)(payloadLen + 2));
    stream.Write(XmpExtHeader.ToArray(), 0, XmpExtHeader.Length);
    stream.Write(guid, 0, 32);
    ByteU.WriteBigEndianUInt32(stream, (uint)fullLength);
    ByteU.WriteBigEndianUInt32(stream, (uint)offset);
    stream.Write(data, dataOffset, dataLength);
  }
}