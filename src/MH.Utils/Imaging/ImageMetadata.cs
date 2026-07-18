using MH.Utils.Imaging.Tiff;
using System;
using System.Text;

namespace MH.Utils.Imaging;

public class ImageMetadata {
  public TiffReader? Reader { get; }

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

  public ImageMetadata(TiffReader? reader) {
    Reader = reader;

    Orientation = _readOrientation();
    UserComment = _readUserComment();
    XpComment = _readXpComment();

    if (_tryReadLatLong(out var lat, out var lng)) {
      Latitude = lat;
      Longitude = lng;
    }
  }

  private ushort? _readOrientation() {
    if (Reader?.GetIfd0().FindEntry(ExifTag.Orientation) is not { } entry) return null;
    return Reader.GetShortValue(entry);
  }

  private string? _readXpComment() {
    if (Reader?.GetIfd0().FindEntry(ExifTag.XpComment) is not { Type: 1 } entry) return null;
    return Reader.ReadUtf16Le(entry.ValueOrOffset, entry.Count);
  }

  private string? _readUserComment() {
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
    
    if (Reader == null) return false;

    var gps = Reader.GetGpsIfd();

    if (gps.FindEntry(ExifTag.GpsLatitudeRef) is not { } latRef
      || gps.FindEntry(ExifTag.GpsLatitude) is not { Type: 5, Count: 3 } lat
      || gps.FindEntry(ExifTag.GpsLongitudeRef) is not { } lngRef
      || gps.FindEntry(ExifTag.GpsLongitude) is not { Type: 5, Count: 3 } lng)
      return false;

    latitude = _readGpsCoordinate(lat.ValueOrOffset);
    longitude = _readGpsCoordinate(lng.ValueOrOffset);

    if (Reader.ReadAsciiChar(latRef.ValueOrOffset, latRef.Count) == 'S')
      latitude = -latitude;

    if (Reader.ReadAsciiChar(lngRef.ValueOrOffset, lngRef.Count) == 'W')
      longitude = -longitude;

    return true;
  }

  private double _readGpsCoordinate(uint offset) {
    double degrees = Reader!.ReadRational(offset);
    double minutes = Reader.ReadRational(offset + 8);
    double seconds = Reader.ReadRational(offset + 16);

    return degrees + minutes / 60.0 + seconds / 3600.0;
  }
}