using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MH.Utils.Imaging.Xmp;

public class XmpMetadata {
  private const int _app1MaxPayload = 65533;
  private const int _paddingChunk = 2048;
  private const string _xmpMetaStart = "<x:xmpmeta";
  private const string _xapMetaStart = "<x:xapmeta";
  private const string _xmpMetaEnd = "</x:xmpmeta>";
  private const string _xapMetaEnd = "</x:xapmeta>";
  private const string _defaultEnd = "\r\n<?xpacket end=\"w\"?>";
  private static string _createDefaultBegin() => $"<?xpacket begin=\"﻿\" id=\"{Guid.NewGuid():N}\"?>\r\n";

  private string? _packetBegin;
  private string? _packetEnd;
  private readonly int _originalPacketSize;

  private static string? _toolkitVersion;

  private MpRegionCollection? _people;

  public XmpDocument? Doc { get; private set; }

  public XmpMetadata(string? packet) {
    if (string.IsNullOrWhiteSpace(packet)) return;

    _originalPacketSize = Encoding.UTF8.GetByteCount(packet);

    var xml = _extractXmp(packet);
    _packetBegin = _extractPacketBegin(packet, xml.Start);
    _packetEnd = _extractPacketEnd(packet, xml.End);
    Doc = new(xml.Xml);
  }

  private readonly record struct XmlSection(int Start, int End, string Xml);

  public XmpDocument EnsureDoc() {
    if (Doc != null) return Doc;

    _packetBegin = _createDefaultBegin();
    _packetEnd = _defaultEnd;
    Doc = new(null);

    return Doc;
  }

  public void SetWidth(ushort? value) {
    EnsureDoc().SetValue(XmpNs.Tiff + "ImageWidth", value?.ToString());
    EnsureDoc().SetValue(XmpNs.Exif + "PixelXDimension", value?.ToString());
  }

  public void SetHeight(ushort? value) {
    EnsureDoc().SetValue(XmpNs.Tiff + "ImageLength", value?.ToString());
    EnsureDoc().SetValue(XmpNs.Exif + "PixelYDimension", value?.ToString());
  }

  public string? GetComment() =>
    Doc?.Rdf.GetXmpLangAlt(XmpNs.Dc + "description");

  public void SetComment(string? value) =>
    EnsureDoc().Rdf.SetXmpLangAlt(XmpNs.Dc + "description", value);

  public int? GetRating() =>
    Doc?.GetInt(XmpNs.Xmp + "Rating");

  public void SetRating(int? value) =>
    EnsureDoc().SetValue(XmpNs.Xmp + "Rating", value?.ToString());

  public string[]? GetKeywords() {
    var items = Doc?
      .GetArray(XmpNs.Dc + "subject")?
      .Select(x => x.Trim())
      .Where(x => x.Length > 0)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return items?.Length == 0 ? null : items;
  }

  public void SetKeywords(string[]? values) {
    EnsureDoc().SetArray(XmpNs.Dc + "subject", values);
    EnsureDoc().SetArray(XmpNs.MicrosoftPhoto + "LastKeywordXMP", values);
  }

  public MpRegionCollection? GetPeople() {
    if (Doc == null) return null;
    _people ??= new(Doc);
    return _people;
  }

  public MpRegionCollection EnsurePeople() {
    _people ??= new(EnsureDoc());
    return _people;
  }

  private static XmlSection _extractXmp(string packet) {
    var start = packet.IndexOf(_xmpMetaStart, StringComparison.Ordinal);
    var endTag = _xmpMetaEnd;

    if (start < 0) {
      start = packet.IndexOf(_xapMetaStart, StringComparison.Ordinal);
      endTag = _xapMetaEnd;
    }

    if (start < 0) throw new InvalidDataException("Missing xmpmeta/xapmeta.");

    var end = packet.IndexOf(endTag, start, StringComparison.Ordinal);
    if (end < 0) throw new InvalidDataException("Missing xmpmeta/xapmeta end tag.");
    end += endTag.Length;

    return new(start, end, packet[start..end]);
  }

  private static string _extractPacketBegin(string packet, int xmlStart) {
    var begin = packet[..xmlStart];

    if (begin.Length == 0)
      begin = _createDefaultBegin();

    return begin;
  }

  private static string _extractPacketEnd(string packet, int xmlEnd) {
    int packetEndStart = packet.IndexOf("<?xpacket", xmlEnd, StringComparison.Ordinal);

    return packetEndStart >= 0 ? packet[packetEndStart..] : _defaultEnd;
  }

  public byte[] ToPacket() {
    _setToolKitVersion();
    _setMetadataDate();

    var xml = EnsureDoc().ToXml();

    var begin = Encoding.UTF8.GetBytes(_packetBegin!);
    var body = Encoding.UTF8.GetBytes(xml);
    var end = Encoding.UTF8.GetBytes(_packetEnd!);

    int targetSize = _calculatePacketSize(begin.Length, body.Length, end.Length);

    using var stream = new MemoryStream(targetSize);
    stream.Write(begin);
    stream.Write(body);
    _writePadding(stream, targetSize - stream.Length);
    stream.Write(end);

    return stream.ToArray();
  }

  private void _setToolKitVersion() {
    EnsureDoc().Document?.Root?.SetAttributeValue(XmpNs.X + "xmptk", _getToolkitVersion());
  }

  private static string _createToolkitVersion() {
    var version = typeof(XmpMetadata).Assembly
      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
      .InformationalVersion ?? "Unknown";

    int i = version.IndexOf('+');
    if (i >= 0) version = version[..i];

    return $"MH.Utils.Imaging {version}";
  }

  private static string _getToolkitVersion() {
    _toolkitVersion ??= _createToolkitVersion();
    return _toolkitVersion;
  }

  private void _setMetadataDate() {
    var dt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
    EnsureDoc().SetValue(XmpNs.Xmp + "ModifyDate", dt);
  }

  private int _calculatePacketSize(int beginLength, int bodyLength, int endLength) {
    int fixedSize = beginLength + bodyLength + endLength;
    int packetSize = Math.Max(_originalPacketSize, fixedSize);

    if (packetSize > _app1MaxPayload)
      return _app1MaxPayload;

    if (packetSize >= fixedSize)
      return packetSize;

    return _growPacketSize(fixedSize);
  }

  private static int _growPacketSize(int requiredSize) {
    int packetSize = requiredSize;

    while (packetSize < _app1MaxPayload) {
      packetSize += _paddingChunk;

      if (packetSize >= requiredSize) break;
    }

    return Math.Min(packetSize, _app1MaxPayload);
  }

  private static void _writePadding(Stream stream, long count) {
    if (count <= 0) return;

    Span<byte> spaces = stackalloc byte[256];
    spaces.Fill((byte)' ');

    while (count > 0) {
      int length = (int)Math.Min(count, spaces.Length);
      stream.Write(spaces[..length]);
      count -= length;
    }
  }
}