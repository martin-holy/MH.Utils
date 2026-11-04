using MH.Utils.Imaging.Exif;

namespace MH.Utils.Imaging.Tiff;

public sealed class TiffFile(TiffIfd ifd0, bool isLittleEndian) {
  public bool IsLittleEndian { get; } = isLittleEndian;
  public TiffIfd Ifd0 { get; } = ifd0;
  public TiffIfd? ExifIfd { get; set; }
  public TiffIfd? GpsIfd { get; set; }

  public static TiffFile CreateEmpty() =>
    new(new TiffIfd(null, []), true);

  public TiffIfd GetOrCreateExifIfd() {
    ExifIfd ??= Ifd0.CreateSubIfd(ExifTag.ExifIfd);
    return ExifIfd;
  }

  public TiffIfd GetOrCreateGpsIfd() {
    GpsIfd ??= Ifd0.CreateSubIfd(ExifTag.GpsIfd);
    return GpsIfd;
  }

  public void EnsurePadding(int paddingChunk) {
    if (ExifIfd?.FindEntry(ExifTag.Padding) is { Value: PaddingValue }) return;

    PaddingValue.ValidatePaddingChunk(paddingChunk);

    GetOrCreateExifIfd().AddEntry(new TiffEntry(ExifTag.Padding, TiffType.Undefined, paddingChunk) {
      Value = new PaddingValue(null, new byte[paddingChunk])
    });
  }
}