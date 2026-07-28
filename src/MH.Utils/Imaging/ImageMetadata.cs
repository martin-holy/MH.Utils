using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Xmp;
using System;
using System.Text;

namespace MH.Utils.Imaging;

public enum UserCommentEncoding { None, Ascii, Unicode, Jis, Undefined }

public class ImageMetadata {
  private readonly string _filePath;
  private TiffFile? _tiffFile;
  private XmpMetadata? _xmp;

  public TiffReader? Reader { get; }
  public TiffFile TiffFile => _getTiffFile();
  public bool IsExifModified { get; private set; }
  public bool IsXmpModified { get; private set; }

  public XmpMetadata Xmp => _xmp ??= new(XmpU.ReadFromJpeg(_filePath));

  public ushort? Width { get => _getWidth(); set => _setWidth(value); }
  public ushort? Height { get => _getHeight(); set => _setHeight(value); }
  public ushort? Orientation { get => _getOrientation(); set => _setOrientation(value); }
  public string? Comment { get => _getComment(); set => _setComment(value); }
  public double? Latitude { get; private set; }
  public double? Longitude { get; private set; }
  public int? Rating { get => _getRating(); set => _setRating(value); }
  public string[]? Keywords => Xmp.GetArray(XmpNs.Dc, "subject");

  public UserCommentEncoding UserCommentEncoding { get; private set; }

  public ImageMetadata(string filePath) {
    _filePath = filePath;
    Reader = JpegTiffReader.ReadFrom(filePath);

    if (_tryReadLatLong(out var lat, out var lng)) {
      Latitude = lat;
      Longitude = lng;
    }
  }

  private ushort? _getWidth() =>
    Reader?.GetIfd0().GetUShort(ExifTag.ImageWidth, Reader.IsLittleEndian);

  private void _setWidth(ushort? value) {
    if (value == Width) return;

    TiffEditor.SetUShort(TiffFile.Ifd0, ExifTag.ImageWidth, value, TiffFile.IsLittleEndian);

    if (TiffFile.ExifIfd != null || value != null)
      TiffEditor.SetUShort(TiffFile.GetOrCreateExifIfd(), ExifTag.PixelXDimension, value, TiffFile.IsLittleEndian);

    Xmp.SetValue(XmpNs.Tiff, "ImageWidth", value?.ToString());
    Xmp.SetValue(XmpNs.Exif, "PixelXDimension", value?.ToString());

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getHeight() =>
    Reader?.GetIfd0().GetUShort(ExifTag.ImageHeight, Reader.IsLittleEndian);

  private void _setHeight(ushort? value) {
    if (value == Height) return;
    
    TiffEditor.SetUShort(TiffFile.Ifd0, ExifTag.ImageHeight, value, TiffFile.IsLittleEndian);

    if (TiffFile.ExifIfd != null || value != null)
      TiffEditor.SetUShort(TiffFile.GetOrCreateExifIfd(), ExifTag.PixelYDimension, value, TiffFile.IsLittleEndian);

    Xmp.SetValue(XmpNs.Tiff, "ImageLength", value?.ToString());
    Xmp.SetValue(XmpNs.Exif, "PixelYDimension", value?.ToString());

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getOrientation() =>
    Reader?.GetIfd0().GetUShort(ExifTag.Orientation, Reader.IsLittleEndian);

  private void _setOrientation(ushort? value) {
    if (value == Orientation) return;
    IsExifModified = true;
    TiffEditor.SetUShort(TiffFile.Ifd0, ExifTag.Orientation, value, TiffFile.IsLittleEndian);
  }

  private string? _getComment() =>
    _readUserComment() ??
    _readXpComment() ??
    Xmp.GetLangAlt(XmpNs.Dc, "description");

  private void _setComment(string? comment) {
    if (comment == Comment) return;

    IsExifModified = true;
    IsXmpModified = true;

    TiffEditor.SetXpComment(TiffFile, comment);
    TiffEditor.SetUserComment(TiffFile, comment, UserCommentEncoding);
    Xmp.SetLangAlt(XmpNs.Dc, "description", comment);
  }

  private string? _readXpComment() {
    if (Reader?.GetIfd0().FindEntry(ExifTag.XpComment) is not { Type: 1 } entry) return null;
    var span = Reader.GetSpan(entry.ValueOrOffset, (int)entry.Count);
    return Encoding.Unicode.GetString(span).TrimEnd('\0');
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
      return Reader.ReadUtf16(span[8..]).TrimEnd('\0');
    }

    if (span[..8].SequenceEqual(ExifU.JisHeader)) {
      UserCommentEncoding = UserCommentEncoding.Jis;
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      return Encoding.GetEncoding("shift_jis").GetString(span[8..]).TrimEnd('\0');
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
    var degrees = Reader!.ReadRational(offset);
    var minutes = Reader.ReadRational(offset + 8);
    var seconds = Reader.ReadRational(offset + 16);

    return GpsU.FromDms(degrees, minutes, seconds);
  }

  private int? _getRating() {
    if (Xmp.GetInt(XmpNs.Xmp, "Rating") is { } rating) return rating;

    return Reader?.GetIfd0().GetUShort(ExifTag.Rating, Reader.IsLittleEndian);
  }

  private void _setRating(int? value) {
    if (value == Rating) return;

    IsXmpModified = true;
    IsExifModified = true;
    
    Xmp.SetValue(XmpNs.Xmp, "Rating", value.ToString());
    TiffEditor.SetUShort(TiffFile.Ifd0, ExifTag.Rating, (ushort?)value, TiffFile.IsLittleEndian);
  }

  public void SetLatLong(double? lat, double? lng) {
    if (_almostEqual(Latitude, lat) && _almostEqual(Longitude, lng)) return;
    IsExifModified = true;
    TiffEditor.SetLatLong(TiffFile, lat, lng);
    Latitude = lat;
    Longitude = lng;
  }

  private static bool _almostEqual(double? a, double? b, double eps = 1e-6) {
    if (a == null || b == null) return a == b;
    return Math.Abs(a.Value - b.Value) < eps;
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

  public bool Write(string srcPath) =>
    JpegTiffWriter.Write(srcPath, _exifToTiff());

  public bool WriteIfModified(string srcPath) {
    if (!IsExifModified) return false;
    return Write(srcPath);
  }

  private byte[] _exifToTiff() {
    var layout = TiffLayoutBuilder.Build(TiffFile, Reader);
    TiffLayoutPlanner.Plan(layout);
    var tiff = TiffSerializer.Serialize(TiffFile, Reader?.IsLittleEndian ?? true);

    // TODO just for test
    if (Reader != null)
      System.IO.File.WriteAllBytes(@"c:\Programs\-=Graphics\ExifTool\_original.tiff", Reader.Buffer.ToArray());
    System.IO.File.WriteAllBytes(@"c:\Programs\-=Graphics\ExifTool\_output.tiff", tiff);

    return tiff;
  }
}