using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpPropertyStorage { Attribute, Element }

public static class XElementExtensions {
  public static string GetAvailablePrefix(this XElement element, XNamespace ns) {
    if (XmpNs.GetPreferredPrefix(ns) is { } preferred
      && element.GetNamespaceOfPrefix(preferred) == null)
      return preferred;

    for (var i = 1; ; i++) {
      var prefix = "ns" + i;

      if (element.GetNamespaceOfPrefix(prefix) == null)
        return prefix;
    }
  }

  public static XElement GetOrCreateXmpDescription(this XElement resource) {
    var dscName = XmpNs.Rdf + "Description";

    if (resource.Name == dscName)
      return resource;

    if (resource.Element(dscName) is { } description)
      return description;

    description = new XElement(dscName);

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
        .Any(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XmpNs.Rdf))
      return;

    description.Remove();
  }

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

    if (description?.Element(name) is { } descriptionElement) {
      if (descriptionElement.Attributes().Any(a => !a.IsNamespaceDeclaration)) {
        descriptionElement.Value = value;
        return;
      }

      descriptionElement.Remove();
      description.SetAttributeValue(name, value);
      return;
    }

    if (description?.Attribute(name) is { } descriptionAttribute) {
      descriptionAttribute.Value = value;
      return;
    }

    if (resource.Element(name) is { } element) {
      if (element.Attributes().Any(a => !a.IsNamespaceDeclaration)) {
        element.Value = value;
        return;
      }

      element.Remove();
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
    var resourceAttribute = resource.Attribute(name);
    var resourceElement = resource.Element(name);
    var descriptionAttribute = description?.Attribute(name);

    if (description?.Element(name) is { } element) {
      element.Value = value;

      resourceAttribute?.Remove();
      resourceElement?.Remove();
      descriptionAttribute?.Remove();

      return;
    }

    if (descriptionAttribute is { }) {
      descriptionAttribute.Remove();

      resourceAttribute?.Remove();
      resourceElement?.Remove();

      description!.Add(new XElement(name, value));
      return;
    }

    if (resourceElement is { } directElement) {
      directElement.Value = value;
      resourceAttribute?.Remove();

      return;
    }

    if (resourceAttribute is { }) {
      resourceAttribute.Remove();

      var target = description ?? resource.GetOrCreateXmpDescription();
      target.Add(new XElement(name, value));

      return;
    }

    // New property.
    (description ?? resource).Add(new XElement(name, value));
  }
}