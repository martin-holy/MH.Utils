using MH.Utils.Imaging.Tiff.Extensions;
using MH.Utils.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

internal static class TiffEditor {
  public static void SetOrientation(TiffFile file, ushort orientation) {
    var data = BinarySpanWriter.GetBytes(orientation, file.IsLittleEndian);
    file.Ifd0.SetEntry(ExifTag.Orientation, TiffType.Short, data);
  }

  public static void SetXpComment(TiffFile file, string? comment) {
    if (string.IsNullOrEmpty(comment)) {
      file.Ifd0.RemoveEntry(ExifTag.XpComment);
      return;
    }

    file.Ifd0.SetEntry(ExifTag.XpComment, TiffType.Byte, Encoding.Unicode.GetBytes(comment + '\0'));
  }

  public static void SetUserComment(TiffFile file, string? comment, UserCommentEncoding encoding) {
    if (string.IsNullOrEmpty(comment)) {
      file.ExifIfd?.RemoveEntry(ExifTag.UserComment);
      return;
    }

    encoding = _normalizeEncoding(comment, encoding);

    byte[] text = encoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(comment),
      UserCommentEncoding.Unicode => file.IsLittleEndian
        ? Encoding.Unicode.GetBytes(comment)
        : Encoding.BigEndianUnicode.GetBytes(comment),
      UserCommentEncoding.Jis => _encodeJis(comment),
      _ => Encoding.UTF8.GetBytes(comment)
    };

    ReadOnlySpan<byte> header = encoding switch {
      UserCommentEncoding.Ascii => ExifU.AsciiHeader,
      UserCommentEncoding.Unicode => ExifU.UnicodeHeader,
      UserCommentEncoding.Jis => ExifU.JisHeader,
      _ => stackalloc byte[8]
    };

    byte[] data = new byte[header.Length + text.Length];

    header.CopyTo(data);
    text.CopyTo(data.AsSpan(header.Length));

    var exifIfd = file.GetOrCreateExifIfd();
    exifIfd.SetEntry(ExifTag.UserComment, TiffType.Undefined, data);
  }

  private static UserCommentEncoding _normalizeEncoding(string text, UserCommentEncoding encoding) {
    encoding = encoding == UserCommentEncoding.None
      ? UserCommentEncoding.Ascii
      : encoding;

    if (encoding == UserCommentEncoding.Ascii && !_isAscii(text))
      encoding = UserCommentEncoding.Unicode;

    return encoding;
  }

  private static bool _isAscii(string text) {
    foreach (char c in text)
      if (c > 0x7F)
        return false;

    return true;
  }

  private static byte[] _encodeJis(string text) {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    return Encoding.GetEncoding("shift_jis").GetBytes(text);
  }

  public static void SetLatLong(TiffFile file, double? lat, double? lng) {
    if (lat == null || lng == null) {
      if (file.GpsIfd == null) return;

      file.Ifd0.RemoveEntry(ExifTag.GpsIfd);
      file.GpsIfd = null;

      return;
    }

    file
      .GetOrCreateGpsIfd()
      .SetAscii(ExifTag.GpsLatitudeRef, lat >= 0 ? "N" : "S")
      .SetRationals(ExifTag.GpsLatitude, GpsU.ToDms(Math.Abs(lat.Value)), file.IsLittleEndian)
      .SetAscii(ExifTag.GpsLongitudeRef, lng >= 0 ? "E" : "W")
      .SetRationals(ExifTag.GpsLongitude, GpsU.ToDms(Math.Abs(lng.Value)), file.IsLittleEndian);
  }
}