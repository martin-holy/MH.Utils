using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Jpeg;
using MH.Utils.Imaging.Xmp;
using System;
using System.IO;
using System.Linq;

namespace MH.Utils.Imaging;

public class ImageMetadata {
  public bool IsExifModified => Jpeg.Exif.IsModified;
  public bool IsXmpModified => Jpeg.Xmp.Doc?.IsModified == true;
  public bool IsModified => IsExifModified || IsXmpModified;

  public JpegFile Jpeg { get; }

  public ushort? Width { get => _getWidth(); set => _setWidth(value); }
  public ushort? Height { get => _getHeight(); set => _setHeight(value); }
  public ExifOrientation? Orientation { get => _getOrientation(); set => _setOrientation(value); }
  public string? Comment { get => _getComment(); set => _setComment(value); }
  public GpsCoordinate? GpsCoordinate { get => _getGpsCoordinate(); set => _setGpsCoordinate(value); }
  public int? Rating { get => _getRating(); set => _setRating(value); }
  public string[]? Keywords { get => _getKeywords(); set => _setKeywords(value); }
  public MpRegionCollection? People { get => _getPeople(); }

  public ImageMetadata(string filePath, JpegMetadataLoad load = JpegMetadataLoad.None) {
    Jpeg = new JpegFile(filePath, load);
  }

  public ImageMetadata(Stream stream, JpegMetadataLoad load = JpegMetadataLoad.None) {
    Jpeg = new JpegFile(stream, load);
  }

  private ushort? _getWidth() =>
    Jpeg.Width;

  private void _setWidth(ushort? value) {
    if (Width == value) return;

    Jpeg.Exif.SetWidth(value);
    Jpeg.Xmp.SetWidth(value);
  }

  private ushort? _getHeight() =>
    Jpeg.Height;

  private void _setHeight(ushort? value) {
    if (Height == value) return;

    Jpeg.Exif.SetHeight(value);
    Jpeg.Xmp.SetHeight(value);
  }

  private ExifOrientation? _getOrientation() =>
    (ExifOrientation?)Jpeg.Exif.GetOrientation();

  private void _setOrientation(ExifOrientation? value) {
    if (Orientation == value) return;

    Jpeg.Exif.SetOrientation((ushort?)value);
  }

  private string? _getComment() =>
    Jpeg.Xmp.GetComment() ?? Jpeg.Exif.GetComment();

  private void _setComment(string? value) {
    if (Comment == value) return;

    Jpeg.Exif.SetComment(value);
    Jpeg.Xmp.SetComment(value);
  }

  private GpsCoordinate? _getGpsCoordinate() =>
    Jpeg.Exif.GetGpsCoordinate();

  public void _setGpsCoordinate(GpsCoordinate? gps) {
    if (gps.AlmostEquals(GpsCoordinate)) return;

    Jpeg.Exif.SetGpsCoordinate(gps);
  }

  private int? _getRating() =>
    Jpeg.Xmp.GetRating() ?? Jpeg.Exif.GetRating();

  private void _setRating(int? value) {
    if (Rating == value) return;

    Jpeg.Xmp.SetRating(value);
    Jpeg.Exif.SetRating(value);
  }

  private string[]? _getKeywords() =>
    Jpeg.Xmp.GetKeywords();

  private void _setKeywords(string[]? value) {
    if ((Keywords ?? []).SequenceEqual(value ?? [])) return;

    Jpeg.Xmp.SetKeywords(value);
  }

  private MpRegionCollection? _getPeople() =>
    Jpeg.Xmp.GetPeople();

  public MpRegionCollection EnsurePeople() =>
    Jpeg.Xmp.EnsurePeople();

  public bool Write(string srcPath) =>
    Jpeg.Write(srcPath);

  public bool Write(Stream source, string destPath) =>
    Jpeg.Write(source, destPath);
}