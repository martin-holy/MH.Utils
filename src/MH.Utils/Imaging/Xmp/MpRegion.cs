using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class MpRegion(XElement element) {
  public XElement Element { get; } = element;

  public string? PersonDisplayName {
    get => (string?)Element.Attribute(XmpNs.MpReg + "PersonDisplayName");
    set => Element.SetAttributeValue(XmpNs.MpReg + "PersonDisplayName", value);
  }

  public string? Rectangle {
    get => (string?)Element.Attribute(XmpNs.MpReg + "Rectangle");
    set => Element.SetAttributeValue(XmpNs.MpReg + "Rectangle", value);
  }
}