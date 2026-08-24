using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public enum XmpValueStyle { Auto, Attribute, Element }

public sealed class XmpDocument(string? xml) {
  private readonly string? _xml = xml;
  private XDocument? _document;

  public bool IsModified { get; private set; }

  public XDocument Document {
    get {
      if (_document != null) return _document;

      _document = string.IsNullOrWhiteSpace(_xml)
        ? _createDocument()
        : XDocument.Parse(_xml, LoadOptions.PreserveWhitespace);

      _document.Changed += (_, _) => IsModified = true;

      return _document;
    }
  }

  private static XDocument _createDocument() {
    var rdf = XmpNs.Rdf;

    return new XDocument(
      new XDeclaration("1.0", "utf-8", null),

      new XElement(XNamespace.Get("adobe:ns:meta/") + "xmpmeta",
        new XAttribute(XNamespace.Xmlns + "x", XmpNs.X.NamespaceName),

        new XElement(rdf + "RDF",
          new XAttribute(XNamespace.Xmlns + "rdf", rdf.NamespaceName),

          new XElement(rdf + "Description",
            new XAttribute(rdf + "about", ""),

            new XAttribute(XNamespace.Xmlns + "xmp", XmpNs.Xmp.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", XmpNs.Dc.NamespaceName)
          )
        )
      )
    );
  }

  public IEnumerable<string>? GetArray(XName name) =>
    Document.GetXmpStringArray(name);

  public void SetArray(XName name, string[]? values) =>
    Document.SetXmpArray(name, values);

  public int? GetInt(XName name) {
    var value = GetValue(name);

    if (int.TryParse(value, out var result))
      return result;

    return null;
  }

  public string? GetValue(XName name) =>
    Document.GetXmpProperty(name);

  public void SetValue(XName name, string? value, XmpValueStyle style = XmpValueStyle.Auto) {
    Document.SetXmpProperty(name, value, style);
  }

  public string? GetLangAlt(XName name) {
    if (GetDescription(name.Namespace)?.Element(name) is not { } property) return null;

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

  public void SetLangAlt(XName name, string? value) {
    var description = GetOrCreateDescription(name.Namespace);

    // Remove an invalid attribute representation.
    description.Attribute(name)?.Remove();

    var property = description.Element(name);

    if (value == null) {
      property?.Remove();
      return;
    }

    var rdf = XmpNs.Rdf;
    var xml = XNamespace.Xml;

    if (property == null) {
      property = new XElement(name);
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

  public XElement? GetDescription(XNamespace ns) =>
    Document
      .Descendants(XmpNs.Rdf + "Description")
      .FirstOrDefault(d =>
        d.Attributes().Any(a => a.Name.Namespace == ns) ||
        d.Elements().Any(e => e.Name.Namespace == ns));

  public XElement GetOrCreateDescription(XNamespace ns) {
    if (GetDescription(ns) is { } desc) return desc;

    var rdf = Document.Descendants(XmpNs.Rdf + "RDF").First();

    desc = rdf.Elements(XmpNs.Rdf + "Description").FirstOrDefault();

    if (desc == null) {
      desc = new XElement(XmpNs.Rdf + "Description",
        new XAttribute(XmpNs.Rdf + "about", ""));

      rdf.Add(desc);
    }

    if (!desc.Attributes().Any(a => a.IsNamespaceDeclaration && a.Value == ns.NamespaceName))
      desc.Add(new XAttribute(XNamespace.Xmlns + XmpNs.GetPrefix(ns), ns.NamespaceName));

    return desc;
  }

  public void RemoveEmptyDescriptions() {
    foreach (var desc in Document.Descendants(XmpNs.Rdf + "Description").ToList()) {
      bool hasContent =
        desc.HasElements ||
        desc.Attributes()
          .Any(a => !a.IsNamespaceDeclaration && a.Name != XmpNs.Rdf + "about");

      if (!hasContent)
        desc.Remove();
    }
  }

  public string ToXml() =>
    Document.ToString(SaveOptions.DisableFormatting);
}