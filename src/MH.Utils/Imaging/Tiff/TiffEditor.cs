using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

internal static class TiffEditor {
  public static void Apply(TiffFile file, ImageMetadata metadata) {
    if (metadata.Orientation is ushort orientation)
      _setOrientation(file, metadata.Reader?.IsLittleEndian ?? true, orientation);

    _setUserComment(file, metadata);
  }

  private static void _setOrientation(TiffFile file, bool littleEndian, ushort orientation) {
    Span<byte> data = stackalloc byte[2];

    if (littleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(data, orientation);
    else
      BinaryPrimitives.WriteUInt16BigEndian(data, orientation);

    var entry = file.Ifd0.FindEntry(ExifTag.Orientation);

    if (entry == null) {
      entry = new TiffEntry((ushort)ExifTag.Orientation, (ushort)TiffType.Short, 1) {
        Value = new InlineValue(null, data.ToArray())
      };

      file.Ifd0.Entries.Add(entry);

      return;
    }

    if (entry.Value is not InlineValue value)
      throw new InvalidOperationException("Orientation is expected to be stored as InlineValue.");

    value.Data = data.ToArray();
  }

  private static void _setUserComment(TiffFile file, ImageMetadata metadata) {
    // TODO write both UserComment and XpComment?
    if (metadata.UserComment == null) return;

    byte[] text = metadata.UserCommentEncoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(metadata.UserComment),
      UserCommentEncoding.Unicode => Encoding.BigEndianUnicode.GetBytes(metadata.UserComment),
      UserCommentEncoding.Jis => _encodeJis(metadata.UserComment),
      _ => Encoding.UTF8.GetBytes(metadata.UserComment)
    };

    ReadOnlySpan<byte> header = metadata.UserCommentEncoding switch {
      UserCommentEncoding.Ascii => ExifU.AsciiHeader,
      UserCommentEncoding.Unicode => ExifU.UnicodeHeader,
      UserCommentEncoding.Jis => ExifU.JisHeader,
      _ => stackalloc byte[8]
    };

    byte[] data = new byte[header.Length + text.Length];

    header.CopyTo(data);
    text.CopyTo(data.AsSpan(header.Length));

    var exifIfd = _getOrCreateExifIfd(file);

    var entry = exifIfd.FindEntry(ExifTag.UserComment);

    if (entry == null) {
      entry = new TiffEntry((ushort)ExifTag.UserComment, (ushort)TiffType.Undefined, (uint)data.Length) {
        Value = new DataValue(null, data)
      };

      exifIfd.Entries.Add(entry);

      return;
    }

    if (entry.Value is not DataValue value)
      throw new InvalidOperationException("UserComment is expected to be stored as DataValue.");

    entry.Count = (uint)data.Length;
    value.Data = data;
  }

  private static byte[] _encodeJis(string text) {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    return Encoding.GetEncoding("shift_jis").GetBytes(text);
  }

  private static TiffIfd _getOrCreateExifIfd(TiffFile file) {
    if (file.ExifIfd != null)
      return file.ExifIfd;

    var exifIfd = new TiffIfd(null, []);

    file.ExifIfd = exifIfd;

    var entry = new TiffEntry(
      (ushort)ExifTag.ExifIfd,
      4, // LONG
      1);

    entry.SubIfd = exifIfd;

    file.Ifd0.Entries.Add(entry);

    return exifIfd;
  }

  private static TiffIfd _getOrCreateGpsIfd(TiffFile file) {
    if (file.GpsIfd != null)
      return file.GpsIfd;

    var gpsIfd = new TiffIfd(null, []);

    file.GpsIfd = gpsIfd;

    var entry = new TiffEntry(
      (ushort)ExifTag.GpsIfd,
      4, // LONG
      1);

    entry.SubIfd = gpsIfd;

    file.Ifd0.Entries.Add(entry);

    return gpsIfd;
  }
}