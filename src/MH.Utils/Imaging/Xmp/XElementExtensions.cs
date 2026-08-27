using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpArrayType { Bag, Seq, Alt }
public enum XmpValueStyle { Auto, Attribute, Element }

public static class XElementExtensions {
  public static bool HasXmpProperty(this XElement element, XName name) =>
    element.Attribute(name) != null || element.Element(name) != null;

  public static string? GetXmpProperty(this XElement element, XName name) {
    if (element.GetXmpPropertyParent(name) is not { } parent)
      return null;

    return parent.Attribute(name)?.Value
      ?? parent.Element(name)?.Value;
  }

  public static XElement? GetXmpPropertyParent(this XElement element, XName name) {
    if (element.HasXmpProperty(name))
      return element;

    return element
      .Descendants(XmpNs.Rdf + "Description")
      .FirstOrDefault(d => d.HasXmpProperty(name));
  }

  public static XElement? GetXmpPropertyElement(this XElement element, XName name) =>
    element.GetXmpPropertyParent(name)?.Element(name);

  public static void SetXmpProperty(this XElement element, XName name, string? value,
    XmpValueStyle style = XmpValueStyle.Auto) {

    var parent = element.GetXmpPropertyParent(name);

    if (value == null) {
      parent?.RemoveXmpProperty(name);
      return;
    }

    parent ??= element;

    var atr = parent.Attribute(name);
    var elm = parent.Element(name);

    if (parent.Element(XmpNs.Rdf + "Description") is { } desc) {
      atr ??= desc.Attribute(name);
      elm ??= desc.Element(name);
    }

    if (style == XmpValueStyle.Auto) {
      style = atr != null
        ? XmpValueStyle.Attribute
        : elm != null
          ? XmpValueStyle.Element
          : XmpValueStyle.Attribute;
    }

    switch (style) {
      case XmpValueStyle.Attribute: parent.SetXmpAttribute(name, value); break;
      case XmpValueStyle.Element: parent.SetXmpElement(name, value); break;
      default: throw new ArgumentOutOfRangeException(nameof(style));
    }
  }

  public static void RemoveXmpProperty(this XElement? element, XName name) {
    if (element == null) return;

    element.Attribute(name)?.Remove();
    element.Element(name)?.Remove();

    if (element.Element(XmpNs.Rdf + "Description") is { } desc) {
      desc.Attribute(name)?.Remove();
      desc.Element(name)?.Remove();
      desc.RemoveEmptyXmpElement();
    }
  }

  public static XElement GetOrCreateXmpPropertyElement(this XElement element, XName name) =>
    element
      .GetOrCreateXmpDescription(name.Namespace)
      .GetOrCreateXmpElement(name);

  public static XElement? GetXmpDescription(this XElement element, XNamespace ns) {
    var dscName = XmpNs.Rdf + "Description";

    if (element.Name == dscName)
      return element;

    if (element.Name == XmpNs.Rdf + "RDF")
      return element
        .Elements(dscName)
        .FirstOrDefault(d =>
          d.Attributes().Any(a => a.Name.Namespace == ns) ||
          d.Elements().Any(e => e.Name.Namespace == ns));

    return element.Element(dscName);
  }

  public static XElement GetOrCreateXmpDescription(this XElement element, XNamespace ns) {
    if (element.GetXmpDescription(ns) is { } desc)
      return desc;

    var isRdf = element.Name == XmpNs.Rdf + "RDF";

    if (isRdf) {
      desc = element.Elements(XmpNs.Rdf + "Description").FirstOrDefault();
      if (desc != null) return desc;
    }

    desc = new XElement(XmpNs.Rdf + "Description");

    if (isRdf) {
      desc.SetAttributeValue(XmpNs.Rdf + "about", "");
      element.Add(desc);

      return desc;
    }

    element.MoveXmpAttributesTo(desc);
    element.MoveXmpElementsTo(desc);
    element.AddFirst(desc);

    return desc;
  }

  public static void MoveXmpAttributesTo(this XElement element, XElement destination) {
    foreach (var attribute in element.Attributes().ToArray()) {
      if (attribute.IsNamespaceDeclaration
        || attribute.Name.Namespace == XmpNs.Rdf
        || attribute.Name.Namespace == XNamespace.Xml)
        continue;

      destination.Add(new XAttribute(attribute));
      attribute.Remove();
    }
  }

  public static void MoveXmpElementsTo(this XElement element, XElement destination) {
    foreach (var child in element.Elements().ToArray()) {
      if (child.Name.Namespace == XmpNs.Rdf)
        continue;

      destination.Add(new XElement(child));
      child.Remove();
    }
  }

  public static void RemoveEmptyXmpDescription(this XElement element) =>
    element.Element(XmpNs.Rdf + "Description")?.RemoveEmptyXmpElement();

  public static void RemoveEmptyXmpElement(this XElement element) {
    if (element.HasElements || element.Attributes()
        .Any(a => !a.IsNamespaceDeclaration && a.Name.Namespace != XmpNs.Rdf))
      return;

    element.Remove();
  }

  public static XElement CreateXmpElement(this XElement parent, XName name) {
    var element = new XElement(name);
    parent.EnsureXmpNamespacePrefix(name.Namespace);
    parent.Add(element);

    return element;
  }

  public static XElement GetOrCreateXmpElement(this XElement parent, XName name) =>
    parent.Element(name) ?? parent.CreateXmpElement(name);

  public static XElement EnsureXmpNamespacePrefix(this XElement element, XNamespace ns) {
    if (element.GetPrefixOfNamespace(ns) == null)
      element.Add(new XAttribute(
        XNamespace.Xmlns + element.GetAvailablePrefix(ns),
        ns.NamespaceName));

    return element;
  }

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

  public static IEnumerable<XElement>? GetXmpArray(this XElement element, XName name) {
    var property = element.GetXmpPropertyElement(name);

    return property?
      .Elements()
      .FirstOrDefault(IsXmpArrayContainer)?
      .Elements(XmpNs.Rdf + "li");
  }

  public static bool IsXmpArrayContainer(this XElement element) =>
    element.Name == XmpNs.Rdf + "Bag" ||
    element.Name == XmpNs.Rdf + "Seq" ||
    element.Name == XmpNs.Rdf + "Alt";

  public static IEnumerable<string>? GetXmpStringArray(this XElement element, XName name) =>
    element.GetXmpArray(name)?.Select(x => x.Value);

  public static XElement? SetXmpArray(this XElement element, XName name,
    IEnumerable<string>? values, XmpArrayType type = XmpArrayType.Bag) {

    var parent = element.GetXmpPropertyParent(name);
    var property = parent?.Element(name);
    var items = values?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

    if (items == null || items.Length == 0) {
      property?.Remove();
      parent?.RemoveEmptyXmpElement();
      return null;
    }

    property ??= element.GetOrCreateXmpPropertyElement(name);
    property.RemoveNodes();

    var array = new XElement(GetXmpArrayName(type));

    foreach (var item in items)
      array.Add(new XElement(XmpNs.Rdf + "li", item));

    property.Add(array);

    return property;
  }

  public static XName GetXmpArrayName(XmpArrayType type) =>
    type switch {
      XmpArrayType.Bag => XmpNs.Rdf + "Bag",
      XmpArrayType.Seq => XmpNs.Rdf + "Seq",
      XmpArrayType.Alt => XmpNs.Rdf + "Alt",
      _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

  public static void SetXmpAttribute(this XElement parent, XName name, string value) {
    var desc = parent.Element(XmpNs.Rdf + "Description");

    if (desc?.Element(name) is { } descElement) {
      if (descElement.Attributes().Any(a => !a.IsNamespaceDeclaration)) {
        descElement.Value = value;
        return;
      }

      descElement.Remove();
      desc.SetAttributeValue(name, value);
      return;
    }

    if (desc?.Attribute(name) is { } descriptionAttribute) {
      descriptionAttribute.Value = value;
      return;
    }

    if (parent.Element(name) is { } element) {
      if (element.Attributes().Any(a => !a.IsNamespaceDeclaration)) {
        element.Value = value;
        return;
      }

      element.Remove();
      parent.SetAttributeValue(name, value);
      return;
    }

    if (parent.Attribute(name) is { } attribute) {
      attribute.Value = value;
      return;
    }

    // New attribute. If a Description already exists, put it there.
    (desc ?? parent).SetAttributeValue(name, value);
  }

  public static void SetXmpElement(this XElement parent, XName name, string value) {
    var desc = parent.Element(XmpNs.Rdf + "Description");
    var atr = parent.Attribute(name);
    var elm = parent.Element(name);
    var descAtr = desc?.Attribute(name);

    if (desc?.Element(name) is { } element) {
      element.Value = value;

      atr?.Remove();
      elm?.Remove();
      descAtr?.Remove();

      return;
    }

    if (descAtr != null) {
      descAtr.Remove();

      atr?.Remove();
      elm?.Remove();

      desc!.Add(new XElement(name, value));
      return;
    }

    if (elm is { } directElm) {
      directElm.Value = value;
      atr?.Remove();

      return;
    }

    if (atr != null) {
      atr.Remove();

      var target = desc ?? parent.GetOrCreateXmpDescription(name.Namespace);
      target.EnsureXmpNamespacePrefix(name.Namespace);
      target.Add(new XElement(name, value));

      return;
    }

    // New property.
    (desc ?? parent).Add(new XElement(name, value));
  }

  public static string? GetXmpLangAlt(this XElement element, XName name) {
    if (element.GetXmpPropertyElement(name) is not { } property) return null;

    if (property.Element(XmpNs.Rdf + "Alt") is not { } alt) return null;

    // Prefer x-default.
    var value = alt.Elements(XmpNs.Rdf + "li")
      .FirstOrDefault(e => (string?)e.Attribute(XNamespace.Xml + "lang") == "x-default");

    if (value != null)
      return value.Value;

    // Otherwise return the first available translation.
    return alt.Elements(XmpNs.Rdf + "li")
      .Select(e => e.Value)
      .FirstOrDefault();
  }

  public static void SetXmpLangAlt(this XElement element, XName name, string? value) {
    // TODO don't create desc if value is null
    var desc = element.GetOrCreateXmpDescription(name.Namespace);

    // Remove an invalid attribute representation.
    desc.Attribute(name)?.Remove();

    var property = desc.Element(name);

    if (value == null) {
      property?.Remove();
      desc.RemoveEmptyXmpElement();
      return;
    }

    property ??= desc.CreateXmpElement(name);

    if (property.Element(XmpNs.Rdf + "Alt") is not { } alt) {
      alt = new XElement(XmpNs.Rdf + "Alt");
      property.RemoveNodes();
      property.Add(alt);
    }

    var item = alt.Elements(XmpNs.Rdf + "li")
      .FirstOrDefault(e => (string?)e.Attribute(XNamespace.Xml + "lang") == "x-default");

    if (item == null) {
      item = new XElement(XmpNs.Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value);
      alt.Add(item);
    }
    else {
      item.Value = value;
    }
  }
}