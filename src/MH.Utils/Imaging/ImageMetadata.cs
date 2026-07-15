using MH.Utils.Imaging.Tiff;
using System;
using System.Text;

namespace MH.Utils.Imaging;

public class ImageMetadata {
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

  public ImageMetadata(TiffReader reader) {
    _tiffReader = reader;

    Orientation = _readOrientation();
    UserComment = _readUserComment();
    XpComment = _readXpComment();

    if (_tryReadLatLong(out var lat, out var lng)) {
      Latitude = lat;
      Longitude = lng;
    }
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

    if (span[..8].SequenceEqual(ExifU.AsciiHeader)) {
      UserCommentEncoding = UserCommentEncoding.Ascii;
      return Encoding.ASCII.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(ExifU.UnicodeHeader)) {
      UserCommentEncoding = UserCommentEncoding.Unicode;
      return Encoding.BigEndianUnicode.GetString(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(ExifU.JisHeader)) {
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