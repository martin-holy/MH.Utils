using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public class XmpMetadata(string? xml) {
  public XmpDocument Doc { get; } = new(xml);

  public void SetWidth(ushort? value) {
    Doc.SetValue(XmpNs.Tiff, "ImageWidth", value?.ToString());
    Doc.SetValue(XmpNs.Exif, "PixelXDimension", value?.ToString());
  }

  public void SetHeight(ushort? value) {
    Doc.SetValue(XmpNs.Tiff, "ImageLength", value?.ToString());
    Doc.SetValue(XmpNs.Exif, "PixelYDimension", value?.ToString());
  }

  public string? GetComment() =>
    Doc.GetLangAlt(XmpNs.Dc, "description");

  public void SetComment(string? value) =>
    Doc.SetLangAlt(XmpNs.Dc, "description", value);

  public int? GetRating() =>
    Doc.GetInt(XmpNs.Xmp, "Rating");

  public void SetRating(int? value) =>
    Doc.SetValue(XmpNs.Xmp, "Rating", value?.ToString());

  public string[]? GetKeywords() =>
    Doc.GetArray(XmpNs.Dc, "subject");

  public void SetKeywords(string[]? values) =>
    Doc.SetArray(XmpNs.Dc, "subject", values);

  public string ToXml() =>
    Doc.Document.ToString(SaveOptions.DisableFormatting);
}