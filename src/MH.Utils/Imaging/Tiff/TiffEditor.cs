using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

internal static class TiffEditor {
  public static void Apply(TiffFile file, ImageMetadata metadata) {
    if (metadata.Orientation is ushort orientation)
      _setOrientation(file, metadata.Reader?.IsLittleEndian ?? true, orientation);

    _setXpComment(file, metadata);
    _setUserComment(file, metadata);
  }

  private static void _setOrientation(TiffFile file, bool littleEndian, ushort orientation) {
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

  private static void _setXpComment(TiffFile file, ImageMetadata metadata) {
    if (metadata.XpComment == null) return;

    byte[] data = Encoding.Unicode.GetBytes(metadata.XpComment + '\0');

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

  private static void _setUserComment(TiffFile file, ImageMetadata metadata) {
    if (metadata.UserComment == null) return;

    var encoding = metadata.UserCommentEncoding == UserCommentEncoding.None
      ? UserCommentEncoding.Ascii
      : metadata.UserCommentEncoding;

    if (encoding == UserCommentEncoding.Ascii && !_isAscii(metadata.UserComment))
      encoding = UserCommentEncoding.Unicode;

    byte[] text = encoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(metadata.UserComment),
      UserCommentEncoding.Unicode => metadata.Reader?.IsLittleEndian == false
        ? Encoding.BigEndianUnicode.GetBytes(metadata.UserComment)
        : Encoding.Unicode.GetBytes(metadata.UserComment),
      UserCommentEncoding.Jis => _encodeJis(metadata.UserComment),
      _ => Encoding.UTF8.GetBytes(metadata.UserComment)
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