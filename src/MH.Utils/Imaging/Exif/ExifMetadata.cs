using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Tiff.Extensions;
using MH.Utils.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Exif;

public enum UserCommentEncoding { None, Ascii, Unicode, Jis, Undefined }

public class ExifMetadata(TiffReader? reader) {
  private TiffFile? _tiffFile;

  public TiffFile TiffFile => _getTiffFile();
  public TiffReader? Reader { get; } = reader;
  public UserCommentEncoding UserCommentEncoding { get; private set; }

  public ushort? GetWidth() =>
    Reader?.GetIfd0().GetUShort(ExifTag.ImageWidth, Reader.IsLittleEndian);

  public void SetWidth(ushort? value) {
    _setUShort(TiffFile.Ifd0, ExifTag.ImageWidth, value);

    if (TiffFile.ExifIfd != null || value != null)
      _setUShort(TiffFile.GetOrCreateExifIfd(), ExifTag.PixelXDimension, value);
  }

  public ushort? GetHeight() =>
    Reader?.GetIfd0().GetUShort(ExifTag.ImageHeight, Reader.IsLittleEndian);

  public void SetHeight(ushort? value) {
    _setUShort(TiffFile.Ifd0, ExifTag.ImageHeight, value);

    if (TiffFile.ExifIfd != null || value != null)
      _setUShort(TiffFile.GetOrCreateExifIfd(), ExifTag.PixelYDimension, value);
  }

  public ushort? GetOrientation() =>
    Reader?.GetIfd0().GetUShort(ExifTag.Orientation, Reader.IsLittleEndian);

  public void SetOrientation(ushort? value) =>
    _setUShort(TiffFile.Ifd0, ExifTag.Orientation, value);

  public string? GetComment() =>
    GetUserComment() ?? GetXpComment();

  public string? GetXpComment() {
    if (Reader?.GetIfd0().FindEntry(ExifTag.XpComment) is not { Type: 1 } entry) return null;
    var span = Reader.GetSpan(entry.ValueOrOffset, (int)entry.Count);
    return Encoding.Unicode.GetString(span).TrimEnd('\0');
  }

  public string? GetUserComment() {
    if (Reader?.GetExifIfd().FindEntry(ExifTag.UserComment) is not { Type: 7 } comment)
      return null;

    if (comment.Count < 8) {
      UserCommentEncoding = UserCommentEncoding.Undefined;
      return string.Empty;
    }

    var span = Reader.GetSpan(comment.ValueOrOffset, (int)comment.Count);

    if (span[..8].SequenceEqual(ExifU.AsciiHeader)) {
      UserCommentEncoding = UserCommentEncoding.Ascii;
      return Encoding.ASCII.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(ExifU.UnicodeHeader)) {
      UserCommentEncoding = UserCommentEncoding.Unicode;
      return Reader.ReadUtf16(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(ExifU.JisHeader)) {
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
      return;
    }

    TiffFile.Ifd0.SetEntry(ExifTag.XpComment, TiffType.Byte, Encoding.Unicode.GetBytes(value + '\0'));
  }

  public void SetUserComment(string? value) {
    if (string.IsNullOrEmpty(value)) {
      TiffFile.ExifIfd?.RemoveEntry(ExifTag.UserComment);
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
      UserCommentEncoding.Ascii => ExifU.AsciiHeader,
      UserCommentEncoding.Unicode => ExifU.UnicodeHeader,
      UserCommentEncoding.Jis => ExifU.JisHeader,
      _ => stackalloc byte[8]
    };

    var data = new byte[header.Length + text.Length];

    header.CopyTo(data);
    text.CopyTo(data.AsSpan(header.Length));

    var exifIfd = TiffFile.GetOrCreateExifIfd();
    exifIfd.SetEntry(ExifTag.UserComment, TiffType.Undefined, data);
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

  public void SetRating(int? value) =>
    _setUShort(TiffFile.Ifd0, ExifTag.Rating, (ushort?)value);

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