using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Xmp;
using System;

namespace MH.Utils.Imaging;

public class ImageMetadata(string filePath) {
  private ExifMetadata? _exif;
  private XmpMetadata? _xmp;

  public bool IsExifModified { get; private set; }
  public bool IsXmpModified { get; private set; }

  public ExifMetadata Exif => _exif ??= new(JpegTiffReader.ReadFrom(filePath));
  public XmpMetadata Xmp => _xmp ??= new(XmpU.ReadFromJpeg(filePath));

  public ushort? Width { get => _getWidth(); set => _setWidth(value); }
  public ushort? Height { get => _getHeight(); set => _setHeight(value); }
  public ushort? Orientation { get => _getOrientation(); set => _setOrientation(value); }
  public string? Comment { get => _getComment(); set => _setComment(value); }
  public GpsCoordinate? GpsCoordinate { get => _getGpsCoordinate(); set => _setGpsCoordinate(value); }
  public int? Rating { get => _getRating(); set => _setRating(value); }
  public string[]? Keywords => Xmp.GetKeywords(); // TODO

  private ushort? _getWidth() =>
    Exif.GetWidth();

  private void _setWidth(ushort? value) {
    if (value == Width) return;

    Exif.SetWidth(value);
    Xmp.SetWidth(value);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getHeight() =>
    Exif.GetHeight();

  private void _setHeight(ushort? value) {
    if (value == Height) return;
    
    Exif.SetHeight(value);
    Xmp.SetHeight(value);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getOrientation() =>
    Exif.GetOrientation();

  private void _setOrientation(ushort? value) {
    if (value == Orientation) return;

    Exif.SetOrientation(value);

    IsExifModified = true;
  }

  private string? _getComment() =>
    Exif.GetComment() ?? Xmp.GetComment();

  private void _setComment(string? comment) {
    if (comment == Comment) return;

    Exif.SetComment(comment);
    Xmp.SetComment(comment);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private GpsCoordinate? _getGpsCoordinate() =>
    Exif.GetGpsCoordinate();

  public void _setGpsCoordinate(GpsCoordinate? gps) {
    var current = GpsCoordinate;

    if (_almostEqual(gps?.Latitude, current?.Latitude) &&
      _almostEqual(gps?.Longitude, current?.Longitude)) return;

    Exif.SetGpsCoordinate(gps);

    IsExifModified = true;
  }

  private static bool _almostEqual(double? a, double? b, double eps = 1e-6) {
    if (a == null || b == null) return a == b;
    return Math.Abs(a.Value - b.Value) < eps;
  }

  private int? _getRating() =>
    Xmp.GetRating() ?? Exif.GetRating();

  private void _setRating(int? value) {
    if (value == Rating) return;

    IsXmpModified = true;
    IsExifModified = true;

    Xmp.SetRating(value);
    Exif.SetRating(value);
  }

  public bool Write(string srcPath) =>
    JpegTiffWriter.Write(srcPath, Exif.ToTiff());

  public bool WriteIfModified(string srcPath) {
    if (!IsExifModified) return false;
    return Write(srcPath);
  }
}