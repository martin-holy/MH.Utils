using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

internal static class TiffEditor {
  public static void SetOrientation(TiffFile file, bool littleEndian, ushort orientation) {
    Span<byte> data = stackalloc byte[2];

    if (littleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(data, orientation);
    else
      BinaryPrimitives.WriteUInt16BigEndian(data, orientation);

    if (file.Ifd0.FindEntry(ExifTag.Orientation) is not { } entry) {
      entry = new TiffEntry(ExifTag.Orientation, TiffType.Short, 1) {
        Value = new InlineValue(null, data.ToArray())
      };

      file.Ifd0.AddEntry(entry);

      return;
    }

    if (entry.Value is not InlineValue value)
      throw new InvalidOperationException("Orientation is expected to be stored as InlineValue.");

    value.Data = data.ToArray();
  }

  public static void SetXpComment(TiffFile file, string? comment) {
    if (string.IsNullOrEmpty(comment)) return; // TODO remove entry

    byte[] data = Encoding.Unicode.GetBytes(comment + '\0');

    if (file.Ifd0.FindEntry(ExifTag.XpComment) is not { } entry) {
      entry = new TiffEntry(ExifTag.XpComment, TiffType.Byte, data.Length) {
        Value = new DataValue(null, data)
      };

      file.Ifd0.AddEntry(entry);

      return;
    }

    if (entry.Type != (ushort)TiffType.Byte)
      throw new InvalidOperationException("XPComment is expected to have type BYTE.");

    if (entry.Value is not DataValue value)
      throw new InvalidOperationException("XPComment is expected to be stored as DataValue.");

    entry.Count = (uint)data.Length;
    value.Data = data;
  }

  public static void SetUserComment(TiffFile file, bool littleEndian, string? comment, UserCommentEncoding encoding) {
    if (string.IsNullOrEmpty(comment)) return; // TODO remove entry

    encoding = encoding == UserCommentEncoding.None
      ? UserCommentEncoding.Ascii
      : encoding;

    if (encoding == UserCommentEncoding.Ascii && !_isAscii(comment))
      encoding = UserCommentEncoding.Unicode;

    byte[] text = encoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(comment),
      UserCommentEncoding.Unicode => littleEndian
        ? Encoding.BigEndianUnicode.GetBytes(comment)
        : Encoding.Unicode.GetBytes(comment),
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

    var exifIfd = _getOrCreateExifIfd(file);

    if (exifIfd.FindEntry(ExifTag.UserComment) is not { } entry) {
      entry = new TiffEntry(ExifTag.UserComment, TiffType.Undefined, data.Length) {
        Value = new DataValue(null, data)
      };

      exifIfd.AddEntry(entry);

      return;
    }

    if (entry.Value is not DataValue value)
      throw new InvalidOperationException("UserComment is expected to be stored as DataValue.");

    entry.Count = (uint)data.Length;
    value.Data = data;
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

  private static TiffIfd _getOrCreateExifIfd(TiffFile file) {
    file.ExifIfd ??= file.Ifd0.CreateIfd(ExifTag.ExifIfd);
    return file.ExifIfd;
  }

  private static TiffIfd _getOrCreateGpsIfd(TiffFile file) {
    file.GpsIfd ??= file.Ifd0.CreateIfd(ExifTag.GpsIfd);
    return file.GpsIfd;
  }
}