using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class MpRegion(XElement element) {
  public XElement Element { get; } = element;

  public string? PersonDisplayName {
    get => Element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName");
    set => _setProperty(XmpNs.MpReg + "PersonDisplayName", value);
  }

  public string? Rectangle {
    get => Element.GetXmpProperty(XmpNs.MpReg + "Rectangle");
    set => _setProperty(XmpNs.MpReg + "Rectangle", value);
  }

  private void _setProperty(XName name, string? value) {
    Element.Attribute(name)?.Remove();
    Element.SetXmpProperty(name, value, XmpValueStyle.Element);
  }
}