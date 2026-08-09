using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public static class XContainerExtensions {
  public static IEnumerable<XElement> GetXmpArray(this XContainer container, XName arrayName) =>
    container
      .Descendants(arrayName)
      .Elements()
      .Where(e =>
        e.Name == XmpNs.Rdf + "Bag" ||
        e.Name == XmpNs.Rdf + "Seq" ||
        e.Name == XmpNs.Rdf + "Alt")
      .Elements(XmpNs.Rdf + "li");
}