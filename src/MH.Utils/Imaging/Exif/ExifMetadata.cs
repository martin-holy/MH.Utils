using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Tiff.Extensions;
using MH.Utils.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MH.Utils.Imaging.Exif;

public enum UserCommentEncoding { None, Ascii, Unicode, Jis, Undefined }

public class ExifMetadata(TiffReader? reader) {
  public static ReadOnlySpan<byte> AsciiHeader => "ASCII\0\0\0"u8;
  public static ReadOnlySpan<byte> UnicodeHeader => "UNICODE\0"u8;
  public static ReadOnlySpan<byte> JisHeader => "JIS\0\0\0\0\0"u8;

  private TiffFile? _tiffFile;

  public TiffFile TiffFile => _getTiffFile();
  public TiffReader? Reader { get; } = reader;
  public UserCommentEncoding UserCommentEncoding { get; private set; }
  public bool IsModified { get; private set; }

  public void UpdateDimensions(ushort width, ushort height) {
    if (Reader == null) return;

    var srcIfd0 = Reader.GetIfd0();
    var destIfd0 = TiffFile.Ifd0;
    _setIfExists(ExifTag.ImageWidth, width, srcIfd0, destIfd0);
    _setIfExists(ExifTag.ImageHeight, height, srcIfd0, destIfd0);

    if (TiffFile.ExifIfd is not { } destExifIfd) return;
    var srcExifIfd = Reader.GetExifIfd();
    _setIfExists(ExifTag.PixelXDimension, width, srcExifIfd, destExifIfd);
    _setIfExists(ExifTag.PixelYDimension, height, srcExifIfd, destExifIfd);
  }

  private void _setIfExists(ExifTag tag, ushort value, TiffEntryData[] src, TiffIfd dest) {
    if (src.GetUShort(tag, Reader!.IsLittleEndian) is { } current && current != value)
      _setUShort(dest, tag, value);
  }

  public ushort? GetOrientation() =>
    Reader?.GetIfd0().GetUShort(ExifTag.Orientation, Reader.IsLittleEndian);

  public void SetOrientation(ushort? value) {
    _setUShort(TiffFile.Ifd0, ExifTag.Orientation, value);

    IsModified = true;
  }

  public DateTime? GetDateTimeOriginal() {
    if (Reader?.GetExifIfd().FindEntry(ExifTag.DateTimeOriginal) is not { Type: (ushort)TiffType.Ascii } entry)
      return null;

    var span = Reader.GetSpan(entry.ValueOrOffset, (int)entry.Count);
    var value = Encoding.ASCII.GetString(span).TrimEnd('\0');

    if (DateTime.TryParseExact(value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
      return date;

    return null;
  }

  public string? GetComment() =>
    GetUserComment() ?? GetXpComment();

  public string? GetXpComment() {
    if (Reader?.GetIfd0().FindEntry(ExifTag.XpComment) is not { Type: 1 } entry) return null;
    var span = Reader.GetValueSpan(entry);
    return Encoding.Unicode.GetString(span).TrimEnd('\0');
  }

  public string? GetUserComment() {
    if (Reader?.GetExifIfd().FindEntry(ExifTag.UserComment) is not { Type: 7 } entry)
      return null;

    if (entry.Count < 8) {
      UserCommentEncoding = UserCommentEncoding.Undefined;
      return string.Empty;
    }

    var span = Reader.GetSpan(entry.ValueOrOffset, (int)entry.Count);

    if (span[..8].SequenceEqual(AsciiHeader)) {
      UserCommentEncoding = UserCommentEncoding.Ascii;
      return Encoding.ASCII.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(UnicodeHeader)) {
      UserCommentEncoding = UserCommentEncoding.Unicode;
      return Reader.ReadUtf16(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(JisHeader)) {
      UserCommentEncoding = UserCommentEncoding.Jis;
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      return Encoding.GetEncoding("shift_jis").GetString(span[8..]).TrimEnd('\0');
    }

    return null;
  }

  public void SetComment(string? value) {
    SetUserComment(value);
    SetXpComment(value);
  }

  public void SetXpComment(string? value) {
    if (string.IsNullOrEmpty(value)) {
      TiffFile.Ifd0.RemoveEntry(ExifTag.XpComment);
      IsModified = true;
      return;
    }

    TiffFile.Ifd0.SetEntry(ExifTag.XpComment, TiffType.Byte, Encoding.Unicode.GetBytes(value + '\0'));
    IsModified = true;
  }

  public void SetUserComment(string? value) {
    if (string.IsNullOrEmpty(value)) {
      TiffFile.ExifIfd?.RemoveEntry(ExifTag.UserComment);
      IsModified = true;
      return;
    }

    var encoding = _normalizeEncoding(value, UserCommentEncoding);

    var text = encoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(value),
      UserCommentEncoding.Unicode => TiffFile.IsLittleEndian
        ? Encoding.Unicode.GetBytes(value)
        : Encoding.BigEndianUnicode.GetBytes(value),
      UserCommentEncoding.Jis => _encodeJis(value),
      _ => Encoding.UTF8.GetBytes(value)
    };

    ReadOnlySpan<byte> header = encoding switch {
      UserCommentEncoding.Ascii => AsciiHeader,
      UserCommentEncoding.Unicode => UnicodeHeader,
      UserCommentEncoding.Jis => JisHeader,
      _ => stackalloc byte[8]
    };

    var data = new byte[header.Length + text.Length];

    header.CopyTo(data);
    text.CopyTo(data.AsSpan(header.Length));

    var exifIfd = TiffFile.GetOrCreateExifIfd();
    exifIfd.SetEntry(ExifTag.UserComment, TiffType.Undefined, data);

    IsModified = true;
  }

  private static UserCommentEncoding _normalizeEncoding(string text, UserCommentEncoding encoding) {
    encoding = encoding == UserCommentEncoding.None
      ? UserCommentEncoding.Ascii
      : encoding;

    if (encoding == UserCommentEncoding.Ascii && !_isAscii(text))
      encoding = UserCommentEncoding.Unicode;

    return encoding;
  }

  private static bool _isAscii(string text) {
    foreach (var c in text)
      if (c > 0x7F)
        return false;

    return true;
  }

  private static byte[] _encodeJis(string text) {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    return Encoding.GetEncoding("shift_jis").GetBytes(text);
  }

  public GpsCoordinate? GetGpsCoordinate() {
    var lat = _readGpsCoordinate(ExifTag.GpsLatitude, ExifTag.GpsLatitudeRef, 'S');
    var lng = _readGpsCoordinate(ExifTag.GpsLongitude, ExifTag.GpsLongitudeRef, 'W');

    if (lat.HasValue && lng.HasValue)
      return new(lat.Value, lng.Value);

    return null;
  }

  public void SetGpsCoordinate(GpsCoordinate? gps) {
    if (gps == null) {
      if (TiffFile.GpsIfd == null) return;

      TiffFile.Ifd0.RemoveEntry(ExifTag.GpsIfd);
      TiffFile.GpsIfd = null;

      IsModified = true;

      return;
    }

    var lat = gps.Value.Latitude;
    var lng = gps.Value.Longitude;

    TiffFile
      .GetOrCreateGpsIfd()
      .SetAscii(ExifTag.GpsLatitudeRef, lat >= 0 ? "N" : "S")
      .SetRationals(ExifTag.GpsLatitude, GpsU.ToDms(Math.Abs(lat)), TiffFile.IsLittleEndian)
      .SetAscii(ExifTag.GpsLongitudeRef, lng >= 0 ? "E" : "W")
      .SetRationals(ExifTag.GpsLongitude, GpsU.ToDms(Math.Abs(lng)), TiffFile.IsLittleEndian);

    IsModified = true;
  }

  private double? _readGpsCoordinate(ExifTag tag, ExifTag refTag, char negRef) {
    if (Reader?.GetGpsIfd() is not { } gps) return null;

    if (gps.FindEntry(refTag) is not { } latRef
      || gps.FindEntry(tag) is not { Type: 5, Count: 3 } lat)
      return null;

    var coordinate = _readGpsCoordinate(lat.ValueOrOffset);

    if (Reader.ReadAsciiChar(latRef.ValueOrOffset, latRef.Count) == negRef)
      coordinate = -coordinate;

    return coordinate;
  }

  private double _readGpsCoordinate(uint offset) {
    var degrees = Reader!.ReadRational(offset);
    var minutes = Reader.ReadRational(offset + 8);
    var seconds = Reader.ReadRational(offset + 16);

    return GpsU.FromDms(degrees, minutes, seconds);
  }

  public ushort? GetRating() =>
    Reader?.GetIfd0().GetUShort(ExifTag.Rating, Reader.IsLittleEndian);

  public void SetRating(int? value) {
    _setUShort(TiffFile.Ifd0, ExifTag.Rating, (ushort?)value);

    IsModified = true;
  }

  public byte[] ToTiff() {
    var layout = TiffLayoutBuilder.Build(TiffFile, Reader);
    TiffLayoutPlanner.Plan(layout);
    var tiff = TiffSerializer.Serialize(TiffFile, Reader?.IsLittleEndian ?? true);

    return tiff;
  }

  private void _setUShort(TiffIfd ifd, ExifTag tag, ushort? value) {
    if (!value.HasValue) {
      ifd.RemoveEntry(tag);
      return;
    }

    var data = BinarySpanWriter.GetBytes(value.Value, TiffFile.IsLittleEndian);
    ifd.SetEntry(tag, TiffType.Short, data);
  }

  private TiffFile _getTiffFile() {
    if (_tiffFile != null) return _tiffFile;

    if (Reader == null) {
      _tiffFile = TiffFile.CreateEmpty();
    }
    else {
      _tiffFile = TiffParser.Parse(Reader);
      TiffResolver.Resolve(Reader, _tiffFile);
    }

    return _tiffFile;
  }
}