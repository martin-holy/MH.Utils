using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Tiff;
using MH.Utils.Imaging.Xmp;
using System;
using System.Linq;

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
  public string[]? Keywords { get => _getKeywords(); set => _setKeywords(value); }

  private ushort? _getWidth() =>
    Exif.GetWidth();

  private void _setWidth(ushort? value) {
    if (Width == value) return;

    Exif.SetWidth(value);
    Xmp.SetWidth(value);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getHeight() =>
    Exif.GetHeight();

  private void _setHeight(ushort? value) {
    if (Height == value) return;
    
    Exif.SetHeight(value);
    Xmp.SetHeight(value);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private ushort? _getOrientation() =>
    Exif.GetOrientation();

  private void _setOrientation(ushort? value) {
    if (Orientation == value) return;

    Exif.SetOrientation(value);

    IsExifModified = true;
  }

  private string? _getComment() =>
    Xmp.GetComment() ?? Exif.GetComment();

  private void _setComment(string? value) {
    if (Comment == value) return;

    Exif.SetComment(value);
    Xmp.SetComment(value);

    IsExifModified = true;
    IsXmpModified = true;
  }

  private GpsCoordinate? _getGpsCoordinate() =>
    Exif.GetGpsCoordinate();

  public void _setGpsCoordinate(GpsCoordinate? gps) {
    if (gps.AlmostEquals(GpsCoordinate)) return;

    Exif.SetGpsCoordinate(gps);

    IsExifModified = true;
  }

  private int? _getRating() =>
    Xmp.GetRating() ?? Exif.GetRating();

  private void _setRating(int? value) {
    if (Rating == value) return;

    IsXmpModified = true;
    IsExifModified = true;

    Xmp.SetRating(value);
    Exif.SetRating(value);
  }

  private string[]? _getKeywords() =>
    Xmp.GetKeywords();

  private void _setKeywords(string[]? value) {
    if ((Keywords ?? []).SequenceEqual(value ?? [])) return;

    IsXmpModified = true;

    Xmp.SetKeywords(value);
  }

  public bool Write(string srcPath) =>
    JpegTiffWriter.Write(srcPath, Exif.ToTiff());

  public bool WriteIfModified(string srcPath) {
    if (!IsExifModified) return false;
    return Write(srcPath);
  }
}