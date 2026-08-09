using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpPropertyStorage { Attribute, Element }

public static class XElementExtensions {
  public static string? GetXmpProperty(this XElement element, XName name) =>
    (string?)element.Attribute(name) ??
    (string?)element.Element(XmpNs.Rdf + "Description")?.Element(name);

  public static void SetXmpProperty(this XElement element, XName name, string? value, XmpPropertyStorage defaultStorage) {
    if (element.Attribute(name) != null) {
      element.SetAttributeValue(name, value);
      return;
    }

    var description = element.Element(XmpNs.Rdf + "Description");

    if (description?.Element(name) is { } property) {
      property.Value = value ?? string.Empty;
      if (value == null)
        property.Remove();

      return;
    }

    if (value == null) return;

    if (defaultStorage == XmpPropertyStorage.Attribute) {
      element.SetAttributeValue(name, value);
      return;
    }

    description ??= _getOrCreateDescription(element);
    description.Add(new XElement(name, value));
  }

  private static XElement _getOrCreateDescription(XElement element) {
    if (element.Element(XmpNs.Rdf + "Description") is { } description)
      return description;

    description = new XElement(XmpNs.Rdf + "Description");

    foreach (var attribute in element.Attributes().ToArray()) {
      if (attribute.IsNamespaceDeclaration) continue;

      description.Add(new XAttribute(attribute));
      attribute.Remove();
    }

    element.AddFirst(description);

    return description;
  }
}