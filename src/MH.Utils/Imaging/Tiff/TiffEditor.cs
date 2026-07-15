using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace MH.Utils.Imaging.Tiff;

internal static class TiffEditor {
  public static void Apply(TiffFile file, ImageMetadata metadata) {
    if (metadata.Orientation is ushort orientation)
      _setOrientation(file, metadata.Reader.IsLittleEndian, orientation);

    _setUserComment(file, metadata);
  }

  private static void _setOrientation(TiffFile file, bool littleEndian, ushort orientation) {
    var entry = file.Ifd0.FindEntry(ExifTag.Orientation);

    if (entry == null)
      return;

    if (entry.Value is not InlineValue value)
      throw new NotImplementedException("Creating Orientation tag is not implemented.");

    Span<byte> data = stackalloc byte[2];

    if (littleEndian)
      BinaryPrimitives.WriteUInt16LittleEndian(data, orientation);
    else
      BinaryPrimitives.WriteUInt16BigEndian(data, orientation);

    value.Data = data.ToArray();
  }

  private static void _setUserComment(TiffFile file, ImageMetadata metadata) {
    // TODO write both UserComment and XpComment?
    if (metadata.UserComment == null) return;

    var entry = file.ExifIfd?.FindEntry(ExifTag.UserComment);

    if (entry == null)
      throw new NotImplementedException("Creating UserComment tag is not implemented.");

    if (entry.Value is not DataValue value)
      throw new InvalidOperationException("UserComment is expected to be stored as DataValue.");

    byte[] text = metadata.UserCommentEncoding switch {
      UserCommentEncoding.Ascii => Encoding.ASCII.GetBytes(metadata.UserComment!),
      UserCommentEncoding.Unicode => Encoding.BigEndianUnicode.GetBytes(metadata.UserComment!),
      UserCommentEncoding.Jis => _encodeJis(metadata.UserComment!),
      _ => Encoding.UTF8.GetBytes(metadata.UserComment!)
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

    entry.Count = (uint)data.Length;
    value.Data = data;
  }

  private static byte[] _encodeJis(string text) {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    return Encoding.GetEncoding("shift_jis").GetBytes(text);
  }
}