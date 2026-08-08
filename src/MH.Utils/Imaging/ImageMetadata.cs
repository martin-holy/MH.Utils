using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Xmp;
using System;
using System.Linq;

namespace MH.Utils.Imaging;

public class ImageMetadata(string filePath) {
  private ExifMetadata? _exif;
  private XmpMetadata? _xmp;

  public bool IsExifModified => Exif.IsModified;
  public bool IsXmpModified => Xmp.Doc.IsModified;

  public ExifMetadata Exif => _exif ??= new(ExifU.ReadFromJpeg(filePath));
  public XmpMetadata Xmp => _xmp ??= new(XmpU.ReadFromJpeg(filePath));

  public ushort? Width { get => _getWidth(); set => _setWidth(value); }
  public ushort? Height { get => _getHeight(); set => _setHeight(value); }
  public ExifOrientation? Orientation { get => _getOrientation(); set => _setOrientation(value); }
  public string? Comment { get => _getComment(); set => _setComment(value); }
  public GpsCoordinate? GpsCoordinate { get => _getGpsCoordinate(); set => _setGpsCoordinate(value); }
  public int? Rating { get => _getRating(); set => _setRating(value); }
  public string[]? Keywords { get => _getKeywords(); set => _setKeywords(value); }
  public MpRegionCollection People { get => _getPeople(); }

  private ushort? _getWidth() =>
    Exif.GetWidth();

  private void _setWidth(ushort? value) {
    if (Width == value) return;

    Exif.SetWidth(value);
    Xmp.SetWidth(value);
  }

  private ushort? _getHeight() =>
    Exif.GetHeight();

  private void _setHeight(ushort? value) {
    if (Height == value) return;
    
    Exif.SetHeight(value);
    Xmp.SetHeight(value);
  }

  private ExifOrientation? _getOrientation() =>
    (ExifOrientation?)Exif.GetOrientation();

  private void _setOrientation(ExifOrientation? value) {
    if (Orientation == value) return;

    Exif.SetOrientation((ushort?)value);
  }

  private string? _getComment() =>
    Xmp.GetComment() ?? Exif.GetComment();

  private void _setComment(string? value) {
    if (Comment == value) return;

    Exif.SetComment(value);
    Xmp.SetComment(value);
  }

  private GpsCoordinate? _getGpsCoordinate() =>
    Exif.GetGpsCoordinate();

  public void _setGpsCoordinate(GpsCoordinate? gps) {
    if (gps.AlmostEquals(GpsCoordinate)) return;

    Exif.SetGpsCoordinate(gps);
  }

  private int? _getRating() =>
    Xmp.GetRating() ?? Exif.GetRating();

  private void _setRating(int? value) {
    if (Rating == value) return;

    Xmp.SetRating(value);
    Exif.SetRating(value);
  }

  private string[]? _getKeywords() =>
    Xmp.GetKeywords();

  private void _setKeywords(string[]? value) {
    if ((Keywords ?? []).SequenceEqual(value ?? [])) return;

    Xmp.SetKeywords(value);
  }

  private MpRegionCollection _getPeople() =>
    Xmp.GetPeople();

  public bool Write(string srcPath) {
    var jpeg = new JpegMetadataWriter();

    if (IsExifModified)
      jpeg.Exif = Exif.ToTiff();

    if (IsXmpModified)
      jpeg.Xmp = Xmp.ToPacket();

    return jpeg.Write(srcPath);
  }

  public bool WriteIfModified(string srcPath) {
    if (!IsExifModified && !IsXmpModified) return false;
    return Write(srcPath);
  }
}