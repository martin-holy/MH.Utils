using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging.Xmp;

[TestClass]
public class XmpDocumentTests {
  [TestMethod]
  public void Document_CreatesEmptyXmpDocument() {
    var document = new XmpDocument(null);

    var xmp = document.Document;

    Assert.IsNotNull(xmp);

    Assert.IsNotNull(xmp.Root);

    Assert.IsNotNull(xmp.Descendants(XmpNs.Rdf + "RDF").Single());

    Assert.IsNotNull(xmp.Descendants(XmpNs.Rdf + "Description").Single());

    Assert.IsFalse(document.IsModified);
  }

  [TestMethod]
  public void Document_ParsesExistingXml() {
    var xml = new XDocument(
      new XElement(XmpNs.Rdf + "RDF",
        new XElement(XmpNs.Rdf + "Description",
          new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"))))
      .ToString();

    var document = new XmpDocument(xml);

    Assert.AreEqual("Martin", document.GetValue(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsFalse(document.IsModified);
  }

  [TestMethod]
  public void IsModified_BecomesTrueWhenDocumentChanges() {
    var document = new XmpDocument(null);

    Assert.IsFalse(document.IsModified);

    document.SetValue(XmpNs.MpReg + "PersonDisplayName", "Martin");

    Assert.IsTrue(document.IsModified);
  }

  [TestMethod]
  public void IsModified_BecomesTrueWhenDocumentIsChangedDirectly() {
    var document = new XmpDocument(null);

    document.Document.Root!.Add(new XElement("Test"));

    Assert.IsTrue(document.IsModified);
  }

  [TestMethod]
  public void GetAndSetValue_UseExtensionMethods() {
    var document = new XmpDocument(null);

    document.SetValue(XmpNs.MpReg + "PersonDisplayName", "Martin");

    Assert.AreEqual("Martin", document.GetValue(XmpNs.MpReg + "PersonDisplayName"));

    document.SetValue(XmpNs.MpReg + "PersonDisplayName", null);

    Assert.IsNull(document.GetValue(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void GetAndSetArray_UseExtensionMethods() {
    var document = new XmpDocument(null);

    document.SetArray(XmpNs.MpReg + "TestArray", ["one", "two"]);

    CollectionAssert.AreEqual(new[] { "one", "two" }, document.GetArray(XmpNs.MpReg + "TestArray")!.ToArray());
  }

  [TestMethod]
  public void GetInt_ReturnsParsedInteger() {
    var document = new XmpDocument(null);

    document.SetValue(XmpNs.Xmp + "Rating", "5");

    Assert.AreEqual(5, document.GetInt(XmpNs.Xmp + "Rating"));
  }

  [TestMethod]
  public void GetInt_ReturnsNullForInvalidValue() {
    var document = new XmpDocument(null);

    document.SetValue(XmpNs.Xmp + "Rating", "abc");

    Assert.IsNull(document.GetInt(XmpNs.Xmp + "Rating"));
  }

  [TestMethod]
  public void GetOrCreateDescription_CreatesDescriptionForNamespace() {
    var document = new XmpDocument(null);
    var description = document.GetOrCreateDescription(XmpNs.MpReg);

    Assert.IsNotNull(description);

    Assert.AreEqual(1, document.Document.Descendants(XmpNs.Rdf + "Description").Count());

    Assert.IsNotNull(description.Attribute(XNamespace.Xmlns + XmpNs.GetPrefix(XmpNs.MpReg)));

    Assert.AreEqual(XmpNs.MpReg.NamespaceName, (string?)description.Attribute(XNamespace.Xmlns + XmpNs.GetPrefix(XmpNs.MpReg)));
  }

  [TestMethod]
  public void GetOrCreateDescription_ReusesExistingDescription() {
    var document = new XmpDocument(null);

    var first = document.GetOrCreateDescription(XmpNs.MpReg);

    var second = document.GetOrCreateDescription(XmpNs.MpReg);

    Assert.AreSame(first, second);

    Assert.AreEqual(1, document.Document.Descendants(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void GetDescription_FindsDescriptionContainingNamespaceProperty() {
    var document = new XmpDocument(null);

    document.SetValue(XmpNs.MpReg + "PersonDisplayName", "Martin");

    var description = document.GetDescription(XmpNs.MpReg);

    Assert.IsNotNull(description);

    Assert.AreEqual("Martin", (string?)description!.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetLangAlt_CreatesXDefault() {
    var document = new XmpDocument(null);

    document.SetLangAlt(XmpNs.Dc + "title", "Martin's pictures");

    var value = document.GetLangAlt(XmpNs.Dc + "title");

    Assert.AreEqual("Martin's pictures", value);

    var description = document.GetDescription(XmpNs.Dc);

    var alt = description!.Element(XmpNs.Dc + "title")!.Element(XmpNs.Rdf + "Alt");

    Assert.IsNotNull(alt);

    var item = alt!.Elements(XmpNs.Rdf + "li").Single();

    Assert.AreEqual("x-default", (string?)item.Attribute(XNamespace.Xml + "lang"));
  }

  [TestMethod]
  public void SetLangAlt_UpdatesExistingXDefault() {
    var document = new XmpDocument(null);

    document.SetLangAlt(XmpNs.Dc + "title", "First");

    document.SetLangAlt(XmpNs.Dc + "title", "Second");

    var description = document.GetDescription(XmpNs.Dc);

    var items = description!
      .Element(XmpNs.Dc + "title")!
      .Element(XmpNs.Rdf + "Alt")!
      .Elements(XmpNs.Rdf + "li")
      .ToArray();

    Assert.AreEqual(1, items.Length);
    Assert.AreEqual("Second", items[0].Value);
  }

  [TestMethod]
  public void GetLangAlt_PrefersXDefault() {
    var document = new XmpDocument(null);

    var description = document.GetOrCreateDescription(XmpNs.Dc);

    var property = new XElement(XmpNs.Dc + "title",
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "cs"), "Czech"),
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "x-default"), "English")));

    description.Add(property);

    Assert.AreEqual("English", document.GetLangAlt(XmpNs.Dc + "title"));
  }

  [TestMethod]
  public void SetLangAlt_Null_RemovesProperty() {
    var document = new XmpDocument(null);

    document.SetLangAlt(XmpNs.Dc + "title", "Hello");

    document.SetLangAlt(XmpNs.Dc + "title", null);

    var description = document.GetDescription(XmpNs.Dc);

    Assert.IsNull(description);
  }

  [TestMethod]
  public void RemoveEmptyDescriptions_RemovesOnlyEmptyDescriptions() {
    var document = new XmpDocument(null);

    var rdf = document.Document.Descendants(XmpNs.Rdf + "RDF").Single();

    var empty = new XElement(XmpNs.Rdf + "Description",
      new XAttribute(XmpNs.Rdf + "about", ""));

    var nonEmpty = new XElement(XmpNs.Rdf + "Description",
      new XAttribute(XmpNs.Rdf + "about", ""),
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    rdf.Add(empty);
    rdf.Add(nonEmpty);

    document.RemoveEmptyDescriptions();

    Assert.IsFalse(rdf.Elements(XmpNs.Rdf + "Description").Contains(empty));

    Assert.IsTrue(rdf.Elements(XmpNs.Rdf + "Description").Contains(nonEmpty));
  }

  [TestMethod]
  public void ToXml_ReturnsDocumentXml() {
    var document = new XmpDocument(null);

    document.SetValue(XmpNs.MpReg + "PersonDisplayName", "Martin");

    var xml = document.ToXml();

    Assert.IsFalse(string.IsNullOrWhiteSpace(xml));
    Assert.IsTrue(xml.Contains("PersonDisplayName"));
    Assert.IsTrue(xml.Contains("Martin"));
  }
}