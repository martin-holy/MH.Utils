using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public static class XContainerExtensions {
  /*public static string? GetXmpProperty(this XContainer container, XName name) {
    var resource = container.GetXmpResource(name);

    return resource == null
      ? null
      : (string?)resource.Attribute(name)
        ?? (string?)resource.Element(name);
  }*/

  /*public static XElement? GetXmpPropertyElement(this XContainer container, XName name) =>
    container.GetXmpResource(name)?.Element(name);*/

  /*public static void SetXmpProperty(this XContainer container, XName name, string? value,
    XmpValueStyle style = XmpValueStyle.Auto) {

    var resource = container.GetXmpResource(name);

    if (value == null) {
      if (resource == null) return;

      resource.Attribute(name)?.Remove();
      resource.Element(name)?.Remove();
      resource.GetXmpDescription()?.Attribute(name)?.Remove();
      resource.GetXmpDescription()?.Element(name)?.Remove();

      resource.RemoveEmptyXmpDescription();
      return;
    }

    resource ??= container.GetOrCreateXmpResource();

    var description = resource.GetXmpDescription();

    var attribute = resource.Attribute(name);
    var element = resource.Element(name);

    if (description != null) {
      attribute ??= description.Attribute(name);
      element ??= description.Element(name);
    }

    if (style == XmpValueStyle.Auto) {
      style = attribute != null
        ? XmpValueStyle.Attribute
        : element != null
          ? XmpValueStyle.Element
          : XmpValueStyle.Attribute;
    }

    switch (style) {
      case XmpValueStyle.Attribute:
        resource.SetXmpAttribute(name, value);
        break;

      case XmpValueStyle.Element:
        resource.SetXmpElement(name, value);
        break;

      default:
        throw new ArgumentOutOfRangeException(nameof(style));
    }
  }*/

  /*public static XElement SetXmpPropertyElement(this XContainer container, XName name) {
    var resource = container.GetOrCreateXmpResource();
    var description = resource.GetOrCreateXmpDescription();

    if (description.Element(name) is { } property)
      return property;

    if (description.GetPrefixOfNamespace(name.Namespace) == null) {
      var prefix = description.GetAvailablePrefix(name.Namespace);
      description.Add(new XAttribute(XNamespace.Xmlns + prefix, name.Namespace.NamespaceName));
    }

    property = new XElement(name);
    description.Add(property);

    return property;
  }*/

  /*public static IEnumerable<XElement>? GetXmpArray(this XContainer container, XName name) {
    var property = container.GetXmpPropertyElement(name);

    return property?
      .Elements()
      .FirstOrDefault(_isXmpArrayContainer)?
      .Elements(XmpNs.Rdf + "li");
  }*/

  /*private static bool _isXmpArrayContainer(XElement element) =>
    element.Name == XmpNs.Rdf + "Bag" ||
    element.Name == XmpNs.Rdf + "Seq" ||
    element.Name == XmpNs.Rdf + "Alt";*/

  /*public static XElement? SetXmpArray(this XContainer container, XName name,
    IEnumerable<string>? values, XmpArrayType type = XmpArrayType.Bag) {

    var items = values?
      .Where(x => !string.IsNullOrWhiteSpace(x))
      .ToArray();

    var property = container.GetXmpPropertyElement(name);

    if (items == null || items.Length == 0) {
      property?.Remove();
      property?.Parent?.RemoveEmptyXmpDescription();
      return null;
    }

    property ??= container.SetXmpPropertyElement(name);
    property.RemoveNodes();

    var array = new XElement(_getXmpArrayName(type));

    foreach (var item in items)
      array.Add(new XElement(XmpNs.Rdf + "li", item));

    property.Add(array);

    return property;
  }

  private static XName _getXmpArrayName(XmpArrayType type) =>
    type switch {
      XmpArrayType.Bag => XmpNs.Rdf + "Bag",
      XmpArrayType.Seq => XmpNs.Rdf + "Seq",
      XmpArrayType.Alt => XmpNs.Rdf + "Alt",
      _ => throw new ArgumentOutOfRangeException(nameof(type))
    };*/

  /*public static XElement? GetXmpResource(this XContainer container, XName name) {
    if (container is XElement element)
      return element.GetXmpResource(name);

    if (container is XDocument document) {
      return document
        .Descendants(XmpNs.Rdf + "Description")
        .FirstOrDefault(d =>
          d.Attribute(name) != null ||
          d.Element(name) != null);
    }

    return null;
  }*/

  /*public static XElement GetOrCreateXmpResource(this XContainer container) {
    if (container is XElement element)
      return element;

    if (container is XDocument document) {
      var description = document
        .Descendants(XmpNs.Rdf + "Description")
        .FirstOrDefault();

      if (description != null)
        return description;

      var rdf = document
        .Descendants(XmpNs.Rdf + "RDF")
        .FirstOrDefault()
        ?? throw new InvalidOperationException("The XMP document does not contain an rdf:RDF element.");

      description = new XElement(XmpNs.Rdf + "Description", new XAttribute(XmpNs.Rdf + "about", ""));

      rdf.Add(description);
      return description;
    }

    throw new InvalidOperationException($"Unsupported XContainer type '{container.GetType().Name}'.");
  }*/

  /*public static IEnumerable<string>? GetXmpStringArray(this XContainer container, XName name) =>
    container
      .GetXmpArray(name)?
      .Select(x => x.Value);*/
}