using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpPropertyStorage { Attribute, Element }

public static class XElementExtensions {
  public static XElement GetOrCreateXmpDescription(this XElement resource) {
    if (resource.Element(XmpNs.Rdf + "Description") is { } description)
      return description;

    description = new XElement(XmpNs.Rdf + "Description");

    // Move all XMP attributes from the resource.
    foreach (var attribute in resource.Attributes().ToArray()) {
      if (attribute.IsNamespaceDeclaration
        || attribute.Name.Namespace == XmpNs.Rdf
        || attribute.Name.Namespace == XNamespace.Xml)
        continue;

      var copy = new XAttribute(attribute);
      description.Add(copy);
      attribute.Remove();
    }

    // Move all XMP property elements from the resource.
    foreach (var element in resource.Elements().ToArray()) {
      if (element.Name.Namespace == XmpNs.Rdf)
        continue;

      var copy = new XElement(element);
      description.Add(copy);
      element.Remove();
    }

    resource.AddFirst(description);

    return description;
  }

  public static XElement? GetXmpDescription(this XElement element) =>
    element.Element(XmpNs.Rdf + "Description");

  public static void RemoveEmptyXmpDescription(this XElement element) {
    var description = element.GetXmpDescription();

    if (description == null
      || description.HasElements
      || description.Attributes()
        .Any(a => !a.IsNamespaceDeclaration && !_isRdfAttribute(a.Name)))
      return;

    description.Remove();
  }

  private static bool _isRdfAttribute(XName name) =>
    name.Namespace == XmpNs.Rdf;

  public static XElement? GetXmpResource(this XElement element, XName name) {
    if (element.Attribute(name) != null
      || element.Element(name) != null)
      return element;

    var description = element.GetXmpDescription();

    if (description?.Attribute(name) != null ||
        description?.Element(name) != null)
      return description;

    return null;
  }

  public static void SetXmpAttribute(this XElement resource, XName name, string value) {
    var description = resource.GetXmpDescription();

    if (description?.Element(name) is { } element) {
      element.Remove();
      description.SetAttributeValue(name, value);
      return;
    }

    if (description?.Attribute(name) is { } descriptionAttribute) {
      descriptionAttribute.Value = value;
      return;
    }

    if (resource.Element(name) is { } directElement) {
      directElement.Remove();

      resource.SetAttributeValue(name, value);
      return;
    }

    if (resource.Attribute(name) is { } attribute) {
      attribute.Value = value;
      return;
    }

    // New attribute. If a Description already exists, put it there.
    (description ?? resource).SetAttributeValue(name, value);
  }

  public static void SetXmpElement(this XElement resource, XName name, string value) {
    var description = resource.GetXmpDescription();

    if (description?.Attribute(name) is { } attribute) {
      attribute.Remove();

      description.Add(new XElement(name, value));
      return;
    }

    if (description?.Element(name) is { } descriptionElement) {
      descriptionElement.Value = value;
      return;
    }

    if (resource.Attribute(name) is { } resourceAttribute) {
      resourceAttribute.Remove();

      var newDescription = resource.GetOrCreateXmpDescription();
      newDescription.Add(new XElement(name, value));
      return;
    }

    if (resource.Element(name) is { } element) {
      element.Value = value;
      return;
    }

    // New property.
    (description ?? resource).Add(new XElement(name, value));
  }
}