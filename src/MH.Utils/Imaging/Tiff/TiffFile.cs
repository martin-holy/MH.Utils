namespace MH.Utils.Imaging.Tiff;

public sealed class TiffFile(TiffIfd ifd0) {
  public TiffIfd Ifd0 { get; } = ifd0;
}