using MH.Utils.IO;
using MH.Utils.Primitives;
using System.Text;

namespace MH.Utils.Imaging.Tiff.Extensions;

public static class TiffIfdExtensions {
  public static TiffIfd SetRationals(this TiffIfd ifd, ExifTag tag, Rational[] values, bool littleEndian) {
    var count = values.Length;
    var data = BinarySpanWriter.GetBytes(values, littleEndian);
    ifd.SetEntry(tag, TiffType.Rational, data, count);
    return ifd;
  }

  public static TiffIfd SetAscii(this TiffIfd ifd, ExifTag tag, string value) {
    ifd.SetEntry(tag, TiffType.Ascii, Encoding.ASCII.GetBytes(value + '\0'));
    return ifd;
  }
}