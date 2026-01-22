using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MH.Utils;

public static class XmpU {
  private static readonly byte[] _xmpHeader = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
  private static readonly byte[] _xmpExtHeader = Encoding.ASCII.GetBytes("http://ns.adobe.com/xmp/extension/\0");
  private static int _normalCapacity = _app1MaxPayload - _xmpHeader.Length;
  private const int _app1MaxPayload = 65533;
  private const int _extChunkDataMax = 65000;
  private const string _extXmpAttr = "HasExtendedXMP=\"";
  private const string _extendedXmpXml =
    @"<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
        <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
          <rdf:Description xmlns:xmpNote=""http://ns.adobe.com/xmp/note/"" xmpNote:HasExtendedXMP=""{0}""/>
        </rdf:RDF>
      </x:xmpmeta>";

  public static string? ReadFromJpeg(string path) {
    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var br = new BinaryReader(fs);

    if (fs.Length < 2) return null;
    if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8) return null; // not JPEG

    byte[]? mainXmpPayload = null;
    string? extendedGuid = null;

    var extChunks = new Dictionary<string, List<(int offset, byte[] data)>>();
    var extFullLength = new Dictionary<string, int>();

    while (fs.Position + 4 < fs.Length) {
      if (br.ReadByte() != 0xFF) continue;

      var marker = br.ReadByte();
      while (marker == 0xFF) marker = br.ReadByte();

      if (marker == 0xDA || marker == 0xD9) break; // SOS or EOI

      int segLen = ByteU.ReadBigEndianUInt16(br);
      if (segLen < 2) break;
      int payloadLen = segLen - 2;

      if (fs.Position + payloadLen > fs.Length) break;

      if (marker != 0xE1) {
        fs.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      var payload = br.ReadBytes(payloadLen);

      // --- Normal XMP ---
      if (ByteU.StartsWith(payload, _xmpHeader)) {
        var xmlOffset = _xmpHeader.Length;
        if (xmlOffset < payload.Length && payload[xmlOffset] == 0x00) xmlOffset++;

        mainXmpPayload = payload[xmlOffset..];

        // try to extract HasExtendedXMP GUID (cheap string scan)
        var xml = _tryDecodeXml(mainXmpPayload, 0, mainXmpPayload.Length);
        if (!string.IsNullOrEmpty(xml)) {
          var i = xml.IndexOf(_extXmpAttr, StringComparison.Ordinal);
          if (i >= 0) {
            var start = i + _extXmpAttr.Length;
            var end = xml.IndexOf('"', start);
            if (end > start) extendedGuid = xml[start..end];
          }
        }

        continue;
      }

      // --- Extended XMP ---
      if (ByteU.StartsWith(payload, _xmpExtHeader)) {
        var p = _xmpExtHeader.Length;
        var guid = Encoding.ASCII.GetString(payload, p, 32);
        p += 32;

        int fullLen = ByteU.ReadBigEndianInt32(payload, ref p);
        int offset = ByteU.ReadBigEndianInt32(payload, ref p);
        var chunk = payload[p..];

        if (!extChunks.TryGetValue(guid, out var list)) {
          list = new List<(int, byte[])>();
          extChunks[guid] = list;
          extFullLength[guid] = fullLen;
        }

        list.Add((offset, chunk));
      }
    }

    // --- Reconstruct Extended XMP if present ---
    if (extendedGuid != null && extChunks.TryGetValue(extendedGuid, out var chunks)) {
      var fullLen = extFullLength[extendedGuid];
      var full = new byte[fullLen];

      foreach (var (offset, data) in chunks) {
        if (offset < 0 || offset + data.Length > full.Length) continue;
        Buffer.BlockCopy(data, 0, full, offset, data.Length);
      }

      return _tryDecodeXml(full, 0, full.Length);
    }

    // --- Fallback to normal XMP ---
    if (mainXmpPayload != null)
      return _tryDecodeXml(mainXmpPayload, 0, mainXmpPayload.Length);

    return null;
  }

  private static string? _tryDecodeXml(byte[] buffer, int offset, int length) {
    if (length <= 0) return null;

    // Check BOM
    if (length >= 3 && buffer[offset] == 0xEF && buffer[offset + 1] == 0xBB && buffer[offset + 2] == 0xBF)
      return Encoding.UTF8.GetString(buffer, offset + 3, length - 3);

    if (length >= 2) {
      // UTF-16 LE BOM
      if (buffer[offset] == 0xFF && buffer[offset + 1] == 0xFE)
        return Encoding.Unicode.GetString(buffer, offset + 2, length - 2);

      // UTF-16 BE BOM
      if (buffer[offset] == 0xFE && buffer[offset + 1] == 0xFF)
        return Encoding.BigEndianUnicode.GetString(buffer, offset + 2, length - 2);
    }

    // Try UTF8 decode, and if it produces a valid-looking XML string, return it.
    try {
      var s = Encoding.UTF8.GetString(buffer, offset, length);
      if (s.Contains("<x:xmpmeta") || s.Contains("<rdf:RDF") || s.Contains("<?xpacket") || s.TrimStart().StartsWith("<"))
        return s;
    }
    catch { }

    // fallback: try UTF-16 LE without BOM (some producers might be inconsistent)
    try {
      var s2 = Encoding.Unicode.GetString(buffer, offset, length);
      if (s2.Contains("<x:xmpmeta") || s2.Contains("<rdf:RDF") || s2.Contains("<?xpacket"))
        return s2;
    }
    catch { }

    return null;
  }

  public static bool WriteToJpeg(string srcPath, string? xmp) {
    var tmpPath = srcPath + ".tmp";
    try {
      using var input = File.OpenRead(srcPath);
      using var output = File.Create(tmpPath);
      WriteToJpeg(input, output, xmp);
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

  public static void WriteToJpeg(Stream input, Stream output, string? xmp) {
    var xmpBytes = xmp == null ? null : Encoding.UTF8.GetBytes(xmp);
    var xmpWritten = false;
    var injectHere = true;

    using var br = new BinaryReader(input);

    // Copy SOI
    ByteU.CopyBytes(input, output, 2);

    while (input.Position + 4 <= input.Length) {
      if (br.ReadByte() != 0xFF) continue;

      var marker = br.ReadByte();
      while (marker == 0xFF) marker = br.ReadByte();

      if (marker == 0xDA) {
        if (!xmpWritten) {
          _writeXmpBytes(output, xmpBytes);
          xmpWritten = true;
        }

        output.WriteByte(0xFF);
        output.WriteByte(0xDA);
        input.CopyTo(output);
        return;
      }

      // Read segment
      var segLen = ByteU.ReadBigEndianUInt16(br);
      if (segLen < 2) throw new InvalidDataException("Invalid JPEG segment length");

      var payloadLen = segLen - 2;
      var payload = br.ReadBytes(payloadLen);

      // Skip existing XMP / Extended XMP
      if (marker == 0xE1 &&
          (ByteU.StartsWith(payload, _xmpHeader) ||
           ByteU.StartsWith(payload, _xmpExtHeader))) {
        continue;
      }

      // Inject XMP after APP0 / APP1 block
      if (!xmpWritten && injectHere && marker != 0xE0 && marker != 0xE1) {
        _writeXmpBytes(output, xmpBytes);
        xmpWritten = true;
        injectHere = false;
      }

      output.WriteByte(0xFF);
      output.WriteByte(marker);
      ByteU.WriteBigEndianUInt16(output, (ushort)(payload.Length + 2));
      output.Write(payload, 0, payload.Length);
    }

    if (!xmpWritten)
      throw new InvalidDataException("Failed to write XMP (no suitable insertion point)");
  }

  private static void _writeXmpBytes(Stream stream, byte[]? xmpBytes) {
    if (xmpBytes == null) return;
    if (xmpBytes.Length <= _normalCapacity)
      _writeApp1(stream, _xmpHeader, xmpBytes);
    else
      _writeExtended(stream, xmpBytes);
  }

  private static void _writeApp1(Stream stream, byte[] header, byte[] payload) {
    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    var len = header.Length + payload.Length + 2;
    ByteU.WriteBigEndianUInt16(stream, (ushort)len);

    stream.Write(header, 0, header.Length);
    stream.Write(payload, 0, payload.Length);
  }

  private static void _writeExtended(Stream stream, byte[] xmlBytes) {
    var guid = Guid.NewGuid().ToString("N");
    var guidBytes = Encoding.ASCII.GetBytes(guid);
    var mainBytes = Encoding.UTF8.GetBytes(string.Format(_extendedXmpXml, guid));
    _writeApp1(stream, _xmpHeader, mainBytes);

    var fullLen = xmlBytes.Length;
    var offset = 0;

    while (offset < fullLen) {
      var chunkLen = Math.Min(_extChunkDataMax, fullLen - offset);
      _writeExtendedChunk(stream, guidBytes, fullLen, offset, xmlBytes, offset, chunkLen);
      offset += chunkLen;
    }
  }

  private static void _writeExtendedChunk(Stream stream, byte[] guid, int fullLength, int offset, byte[] data, int dataOffset, int dataLength) {
    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    int payloadLen =
      _xmpExtHeader.Length +
      32 + // GUID
      4 +  // full length
      4 +  // offset
      dataLength;

    ByteU.WriteBigEndianUInt16(stream, (ushort)(payloadLen + 2));
    stream.Write(_xmpExtHeader, 0, _xmpExtHeader.Length);
    stream.Write(guid, 0, 32);
    ByteU.WriteBigEndianUInt32(stream, (uint)fullLength);
    ByteU.WriteBigEndianUInt32(stream, (uint)offset);
    stream.Write(data, dataOffset, dataLength);
  }

  public static string UpdateDimensions(string xmp, int width, int height) {
    xmp = _regexReplace(xmp, @"(tiff:ImageWidth\s*=\s*"")[^""]*(""?)", $"$1{width}$2");
    xmp = _regexReplace(xmp, @"(tiff:ImageLength\s*=\s*"")[^""]*(""?)", $"$1{height}$2");
    xmp = _regexReplace(xmp, @"(exif:PixelXDimension\s*=\s*"")[^""]*(""?)", $"$1{width}$2");
    xmp = _regexReplace(xmp, @"(exif:PixelYDimension\s*=\s*"")[^""]*(""?)", $"$1{height}$2");
    return xmp;
  }

  private static string _regexReplace(string input, string pattern, string replacement) {
    try {
      return Regex.Replace(input, pattern, replacement, RegexOptions.IgnoreCase);
    }
    catch {
      return input;
    }
  }
}