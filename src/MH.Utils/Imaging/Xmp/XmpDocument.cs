using System;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpValueStyle { Auto, Attribute, Element }

public sealed class XmpDocument(string? xml) {
  private readonly string? _xml = xml;
  private XDocument? _document;

  public XDocument Document =>
    _document ??= string.IsNullOrWhiteSpace(_xml)
      ? _createDocument()
      : XDocument.Parse(_xml, LoadOptions.PreserveWhitespace);

  private static XDocument _createDocument() {
    throw new NotImplementedException(); //TODO
  }

  public string[]? GetArray(XNamespace ns, string name) {
    var items = Document
      .Descendants()
      .Where(e => e.Name == ns + name)
      .Descendants()
      .Where(e => e.Name.LocalName == "li")
      .Select(e => e.Value.Trim())
      .Where(x => x.Length > 0)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return items.Length == 0 ? null : items;
  }

  public void SetArray(XNamespace ns, string name, string[]? values) {
    var bag = _findArray(ns, name);

    if (values == null || values.Length == 0) {
      bag?.Parent?.Remove();
      return;
    }

    if (bag == null) {
      var description = _getOrCreateDescription(ns);
      var element = new XElement(ns + name, new XElement(XmpNs.Rdf + "Bag"));

      description.Add(element);
      bag = element.Element(XmpNs.Rdf + "Bag")!;
    }

    bag.RemoveNodes();

    foreach (var value in values)
      bag.Add(new XElement(XmpNs.Rdf + "li", value));
  }

  private XElement? _findArray(XNamespace ns, string name) =>
    Document
      .Descendants(ns + name)
      .Elements(XmpNs.Rdf + "Bag")
      .FirstOrDefault();

  public int? GetInt(XNamespace ns, string name) {
    var value = GetValue(ns, name);

    if (int.TryParse(value, out var result))
      return result;

    return null;
  }

  public string? GetValue(XNamespace ns, string name) {
    return Document
      .Descendants()
      .Attributes()
      .FirstOrDefault(a => a.Name == ns + name)
      ?.Value
      ?? Document
        .Descendants()
        .FirstOrDefault(e => e.Name == ns + name)
        ?.Value;
  }

  public void SetValue(XNamespace ns, string name, string? value,
    XmpValueStyle defaultStyle = XmpValueStyle.Attribute,
    XmpValueStyle style = XmpValueStyle.Auto) {

    var description = _getOrCreateDescription(ns);
    var attribute = description.Attribute(ns + name);
    var element = description.Element(ns + name);

    if (value == null) {
      attribute?.Remove();
      element?.Remove();
      return;
    }

    if (style == XmpValueStyle.Auto) {
      if (attribute != null)
        style = XmpValueStyle.Attribute;
      else if (element != null)
        style = XmpValueStyle.Element;
      else
        style = defaultStyle;
    }

    switch (style) {
      case XmpValueStyle.Attribute:
        description.SetAttributeValue(ns + name, value);
        element?.Remove();
        break;

      case XmpValueStyle.Element:
        if (element == null) {
          element = new XElement(ns + name);
          description.Add(element);
        }

        element.Value = value;
        attribute?.Remove();
        break;

      default:
        throw new InvalidOperationException(
          $"Unsupported XMP value style '{style}'.");
    }
  }

  public string? GetLangAlt(XNamespace ns, string name) {
    if (_getOrCreateDescription(ns).Element(ns + name) is not { } property) return null;

    var rdf = XmpNs.Rdf;
    var xml = XNamespace.Xml;

    if (property.Element(rdf + "Alt") is not { } alt) return null;

    // Prefer x-default.
    var value = alt.Elements(rdf + "li")
      .FirstOrDefault(e => (string?)e.Attribute(xml + "lang") == "x-default");

    if (value != null)
      return value.Value;

    // Otherwise return the first available translation.
    return alt.Elements(rdf + "li")
      .Select(e => e.Value)
      .FirstOrDefault();
  }

  public void SetLangAlt(XNamespace ns, string name, string? value) {
    var description = _getOrCreateDescription(ns);

    // Remove an invalid attribute representation.
    description.Attribute(ns + name)?.Remove();

    var property = description.Element(ns + name);

    if (value == null) {
      property?.Remove();
      return;
    }

    var rdf = XmpNs.Rdf;
    var xml = XNamespace.Xml;

    if (property == null) {
      property = new XElement(ns + name);
      description.Add(property);
    }

    if (property.Element(rdf + "Alt") is not { } alt) {
      alt = new XElement(rdf + "Alt");
      property.RemoveNodes();
      property.Add(alt);
    }

    var item = alt.Elements(rdf + "li")
      .FirstOrDefault(e => (string?)e.Attribute(xml + "lang") == "x-default");

    if (item == null) {
      item = new XElement(rdf + "li", new XAttribute(xml + "lang", "x-default"), value);
      alt.Add(item);
    }
    else {
      item.Value = value;
    }
  }

  private XElement _getOrCreateDescription(XNamespace ns) {
    var desc = Document
      .Descendants(XmpNs.Rdf + "Description")
      .FirstOrDefault(d =>
        d.Attributes()
          .Any(a => a.IsNamespaceDeclaration && a.Value == ns.NamespaceName));

    if (desc != null) return desc;

    var rdf = Document.Descendants(XmpNs.Rdf + "RDF").First();

    desc = new XElement(
      XmpNs.Rdf + "Description",
      new XAttribute(XmpNs.Rdf + "about", ""),
      new XAttribute(XNamespace.Xmlns + XmpNs.GetPrefix(ns), ns.NamespaceName));

    rdf.Add(desc);

    return desc;
  }
}