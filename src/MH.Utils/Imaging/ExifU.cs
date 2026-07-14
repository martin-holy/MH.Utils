using MH.Utils.Imaging.Tiff;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MH.Utils.Imaging;

public readonly record struct ExifEntry(uint EntryOffset, ushort Tag, ushort Type, uint Count, uint ValueOrOffset);

public enum UserCommentEncoding { None, Ascii, Unicode, Jis, Undefined }

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

  ThumbnailOffset = 0x0201,
  ThumbnailLength = 0x0202,

  Padding = 0xEA1C
}

public sealed class ExifU {
  public static ReadOnlySpan<byte> ExifHeader => "Exif\0\0"u8;
  public static ReadOnlySpan<byte> AsciiHeader => "ASCII\0\0\0"u8;
  public static ReadOnlySpan<byte> UnicodeHeader => "UNICODE"u8;
  public static ReadOnlySpan<byte> JisHeader => "JIS\0\0\0\0\0"u8;

  private readonly TiffReader _tiffReader;

  public TiffReader Reader => _tiffReader;

  public ushort? Orientation { get; set; }
  public string? UserComment { get; set; }
  public string? XpComment { get; set; }
  public string? Comment {
    get => XpComment ?? UserComment;
    set {
      XpComment = value;
      UserComment = value;
    }
  }
  public double? Latitude { get; set; }
  public double? Longitude { get; set; }

  public UserCommentEncoding UserCommentEncoding { get; private set; }

  private ExifU(byte[] tiff) {
    _tiffReader = new(tiff);

    Orientation = _readOrientation();
    UserComment = _readUserComment();
    XpComment = _readXpComment();

    if (_tryReadLatLong(out var lat, out var lng)) {
      Latitude = lat;
      Longitude = lng;
    }
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

    Span<byte> header = stackalloc byte[ExifHeader.Length];

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

      if (payloadLen < ExifHeader.Length) {
        stream.Seek(payloadLen, SeekOrigin.Current);
        continue;
      }

      stream.ReadExactly(header);

      if (!header.SequenceEqual(ExifHeader)) {
        stream.Seek(payloadLen - header.Length, SeekOrigin.Current);
        continue;
      }

      return new ExifU(br.ReadBytes(payloadLen - header.Length));
    }

    return null;
  }

  public static bool WriteToJpeg(string srcPath, ExifU? exif) {
    var tmpPath = srcPath + ".tmp";

    try {
      using (var input = File.OpenRead(srcPath))
      using (var output = File.Create(tmpPath)) {
        WriteToJpeg(input, output, exif);
      }

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

  public static void WriteToJpeg(Stream input, Stream output, ExifU? exif) {
    using var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: true);

    // SOI
    ByteU.CopyBytes(input, output, 2);

    bool exifWritten = false;
    bool injectHere = true;

    while (input.Position + 4 <= input.Length) {
      if (br.ReadByte() != 0xFF)
        continue;

      byte marker = br.ReadByte();

      while (marker == 0xFF)
        marker = br.ReadByte();

      // Start of Scan
      if (marker == 0xDA) {
        if (!exifWritten) {
          _writeExif(output, exif);
          exifWritten = true;
        }

        output.WriteByte(0xFF);
        output.WriteByte(0xDA);

        input.CopyTo(output);
        return;
      }

      ushort segLen = ByteU.ReadBigEndianUInt16(br);

      if (segLen < 2)
        throw new InvalidDataException("Invalid JPEG segment.");

      int payloadLen = segLen - 2;

      // Existing EXIF?
      if (marker == 0xE1) {
        Span<byte> header = stackalloc byte[ExifHeader.Length];

        if (payloadLen >= header.Length) {
          input.ReadExactly(header);

          if (header.SequenceEqual(ExifHeader)) {
            // Skip old EXIF
            input.Seek(payloadLen - header.Length, SeekOrigin.Current);
            continue;
          }

          // Not EXIF, restore consumed header
          output.WriteByte(0xFF);
          output.WriteByte(marker);
          ByteU.WriteBigEndianUInt16(output, segLen);

          output.Write(header);

          ByteU.CopyBytes(input, output, payloadLen - header.Length);

          injectHere = false;

          continue;
        }
      }

      if (!exifWritten && injectHere && marker != 0xE0 && marker != 0xE1) {
        _writeExif(output, exif);
        exifWritten = true;
        injectHere = false;
      }

      output.WriteByte(0xFF);
      output.WriteByte(marker);
      ByteU.WriteBigEndianUInt16(output, segLen);

      ByteU.CopyBytes(input, output, payloadLen);
    }

    if (!exifWritten)
      throw new InvalidDataException("Failed to write EXIF.");
  }

  private static void _writeExif(Stream stream, ExifU? exif) {
    if (exif == null) return;

    var file = TiffParser.Parse(exif.Reader);
    TiffResolver.Resolve(exif.Reader, file);
    TiffEditor.Apply(file, exif);
    var layout = TiffLayoutBuilder.Build(file, exif.Reader);
    TiffLayoutPlanner.Plan(layout);
    byte[] tiff = TiffSerializer.Serialize(layout, exif.Reader.IsLittleEndian);

    stream.WriteByte(0xFF);
    stream.WriteByte(0xE1);

    ushort len = (ushort)(ExifHeader.Length + tiff.Length + 2);

    if (len > 65533)
      throw new InvalidOperationException("EXIF is too large for a JPEG APP1 segment.");

    ByteU.WriteBigEndianUInt16(stream, len);

    stream.Write(ExifHeader);
    stream.Write(tiff);
  }

  private ushort? _readOrientation() {
    if (TiffReader.FindEntry(_tiffReader.GetIfd0(), ExifTag.Orientation) is not { } entry) return null;
    return _tiffReader.GetShortValue(entry);
  }

  private string? _readXpComment() {
    if (TiffReader.FindEntry(_tiffReader.GetIfd0(), ExifTag.XpComment) is not { Type: 1 } entry) return null;
    return _tiffReader.ReadUtf16Le(entry.ValueOrOffset, entry.Count);
  }

  private string? _readUserComment() {
    if (TiffReader.FindEntry(_tiffReader.GetExifIfd(), ExifTag.UserComment) is not { Type: 7 } comment)
      return null;

    if (comment.Count < 8) {
      UserCommentEncoding = UserCommentEncoding.Undefined;
      return string.Empty;
    }

    var span = _tiffReader.GetSpan(comment.ValueOrOffset, (int)comment.Count);

    if (span[..8].SequenceEqual(AsciiHeader)) {
      UserCommentEncoding = UserCommentEncoding.Ascii;
      return Encoding.ASCII.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(UnicodeHeader)) {
      UserCommentEncoding = UserCommentEncoding.Unicode;
      return Encoding.BigEndianUnicode.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(JisHeader)) {
      UserCommentEncoding = UserCommentEncoding.Jis;
      return null; // Not implemented.
    }

    return null;
  }

  private bool _tryReadLatLong(out double latitude, out double longitude) {
    latitude = 0;
    longitude = 0;

    var gps = _tiffReader.GetGpsIfd();

    if (TiffReader.FindEntry(gps, ExifTag.GpsLatitudeRef) is not { } latRef
      || TiffReader.FindEntry(gps, ExifTag.GpsLatitude) is not { Type: 5, Count: 3 } lat
      || TiffReader.FindEntry(gps, ExifTag.GpsLongitudeRef) is not { } lngRef
      || TiffReader.FindEntry(gps, ExifTag.GpsLongitude) is not { Type: 5, Count: 3 } lng)
      return false;

    latitude = _readGpsCoordinate(lat.ValueOrOffset);
    longitude = _readGpsCoordinate(lng.ValueOrOffset);

    if (_tiffReader.ReadAsciiChar(latRef.ValueOrOffset, latRef.Count) == 'S')
      latitude = -latitude;

    if (_tiffReader.ReadAsciiChar(lngRef.ValueOrOffset, lngRef.Count) == 'W')
      longitude = -longitude;

    return true;
  }

  private double _readGpsCoordinate(uint offset) {
    double degrees = _tiffReader.ReadRational(offset);
    double minutes = _tiffReader.ReadRational(offset + 8);
    double seconds = _tiffReader.ReadRational(offset + 16);

    return degrees + minutes / 60.0 + seconds / 3600.0;
  }
}