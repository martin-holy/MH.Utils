using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class MpRegion(XElement element) {
  public XElement Element { get; } = element;

  public string? PersonDisplayName {
    get => Element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName");
    set => Element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", value);
  }

  public string? Rectangle {
    get => Element.GetXmpProperty(XmpNs.MpReg + "Rectangle");
    set => Element.SetXmpProperty(XmpNs.MpReg + "Rectangle", value);
  }
}