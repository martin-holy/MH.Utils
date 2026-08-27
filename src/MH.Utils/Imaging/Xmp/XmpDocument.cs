using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class XmpDocument(string? xml) {
  private readonly string? _xml = xml;
  private XDocument? _document;
  private XElement? _rdf;

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

  public XElement Rdf {
    get {
      _rdf ??= Document.Root?.Element(XmpNs.Rdf + "RDF");
      if (_rdf == null) throw new InvalidOperationException("XMP document has no RDF element.");
      return _rdf;
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
    Rdf.GetXmpStringArray(name);

  public void SetArray(XName name, string[]? values) =>
    Rdf.SetXmpArray(name, values);

  public int? GetInt(XName name) =>
    int.TryParse(GetValue(name), out var result) ? result : null;

  public string? GetValue(XName name) =>
    Rdf.GetXmpProperty(name);

  public void SetValue(XName name, string? value, XmpValueStyle style = XmpValueStyle.Auto) =>
    Rdf.SetXmpProperty(name, value, style);

  public void RemoveEmptyDescriptions() {
    foreach (var desc in Rdf.Descendants(XmpNs.Rdf + "Description").ToArray())
      desc.RemoveEmptyXmpElement();
  }

  public string ToXml() =>
    Document.ToString(SaveOptions.DisableFormatting);
}