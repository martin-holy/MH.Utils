using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging.Xmp;

[TestClass]
public class XmpExtensionsTests {
  private static readonly XName _rectangleKeywords = XNamespace.Get("urn:martin-holy:xmp-test") + "RectangleKeywords";

  [TestMethod]
  public void GetXmpProperty_ReadsAttribute() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    Assert.AreEqual("Martin", element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void GetXmpProperty_ReadsDescriptionAttribute() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.Rdf + "Description",
        new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin")));

    Assert.AreEqual("Martin", element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void GetXmpProperty_ReadsElement() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    Assert.AreEqual("Martin", element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void GetXmpProperty_ReadsDescriptionElement() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.Rdf + "Description",
        new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin")));

    Assert.AreEqual("Martin", element.GetXmpProperty(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Auto_PreservesExistingAttribute() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin2", XmpValueStyle.Auto);

    Assert.AreEqual("Martin2", (string?)element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Element(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Auto_PreservesExistingElement() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin2", XmpValueStyle.Auto);

    Assert.AreEqual("Martin2", (string?)element.Element(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Auto_NewProperty_UsesAttribute() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin", XmpValueStyle.Auto);

    Assert.AreEqual("Martin", (string?)element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Element(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Element_UsesElement() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin", XmpValueStyle.Element);

    Assert.AreEqual("Martin", (string?)element.Element(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Attribute_ReplacesExistingElement() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin2", XmpValueStyle.Attribute);

    Assert.AreEqual("Martin2", (string?)element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Element(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Attribute_PreservesElementWithAttributes() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName",
        new XAttribute("foo", "bar"), "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", "Martin2", XmpValueStyle.Attribute);

    var property = element.Element(XmpNs.MpReg + "PersonDisplayName");

    Assert.IsNotNull(property);

    Assert.AreEqual("Martin2", property!.Value);

    Assert.AreEqual("bar", (string?)property.Attribute("foo"));

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void SetXmpProperty_Attribute_PreservesXmlLang() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.Dc + "title",
        new XAttribute(XNamespace.Xml + "lang", "en"), "Hello"));

    element.SetXmpProperty(XmpNs.Dc + "title", "Hello world", XmpValueStyle.Attribute);

    var property = element.Element(XmpNs.Dc + "title");

    Assert.IsNotNull(property);

    Assert.AreEqual("Hello world", property!.Value);

    Assert.AreEqual("en", (string?)property.Attribute(XNamespace.Xml + "lang"));

    Assert.IsNull(element.Attribute(XmpNs.Dc + "title"));
  }

  [TestMethod]
  public void SetXmpProperty_Null_RemovesAttribute() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", null);

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }


  [TestMethod]
  public void SetXmpProperty_Null_RemovesElement() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin"));

    element.SetXmpProperty(XmpNs.MpReg + "PersonDisplayName", null);

    Assert.IsNull(element.Element(XmpNs.MpReg + "PersonDisplayName"));
  }


  [TestMethod]
  public void SetXmpPropertyElement_MovesAttributePropertiesIntoDescription() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XAttribute(XmpNs.MpReg + "Rectangle", "0,0,1,1"));

    var property = element.GetOrCreateXmpPropertyElement(_rectangleKeywords);

    Assert.IsNotNull(property);

    var description = element.Element(XmpNs.Rdf + "Description");

    Assert.IsNotNull(description);

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "Rectangle"));

    Assert.AreEqual("Martin", (string?)description!.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.AreEqual("0,0,1,1", (string?)description.Attribute(XmpNs.MpReg + "Rectangle"));

    Assert.AreSame(property, description.Element(_rectangleKeywords));
  }

  [TestMethod]
  public void SetXmpPropertyElement_MovesElementPropertiesIntoDescription() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XElement(XmpNs.MpReg + "Rectangle", "0,0,1,1"));

    element.GetOrCreateXmpPropertyElement(_rectangleKeywords);

    var description = element.Element(XmpNs.Rdf + "Description");

    Assert.IsNotNull(description);

    Assert.IsNull(element.Element(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Element(XmpNs.MpReg + "Rectangle"));

    Assert.AreEqual("Martin", (string?)description!.Element(XmpNs.MpReg + "PersonDisplayName"));

    Assert.AreEqual("0,0,1,1", (string?)description.Element(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void SetXmpPropertyElement_MovesMixedPropertiesIntoDescription() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XElement(XmpNs.MpReg + "Rectangle", "0,0,1,1"));

    element.GetOrCreateXmpPropertyElement(_rectangleKeywords);

    var description = element.Element(XmpNs.Rdf + "Description");

    Assert.IsNotNull(description);

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Element(XmpNs.MpReg + "Rectangle"));

    Assert.AreEqual("Martin", (string?)description!.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.AreEqual("0,0,1,1", (string?)description.Element(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void SetXmpPropertyElement_ReusesExistingDescription() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XElement(XmpNs.Rdf + "Description",
        new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin")));

    var description = element.Element(XmpNs.Rdf + "Description");
    var property = element.GetOrCreateXmpPropertyElement(_rectangleKeywords);

    Assert.AreSame(description, element.Element(XmpNs.Rdf + "Description"));

    Assert.AreSame(property, description!.Element(_rectangleKeywords));

    Assert.AreEqual(1, element.Elements(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void SetXmpArray_CreatesBag() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpArray(_rectangleKeywords, ["one", "two"]);

    var values = element.GetXmpStringArray(_rectangleKeywords)?.ToArray();

    CollectionAssert.AreEqual(new[] { "one", "two" }, values);

    var bag = element.GetXmpPropertyElement(_rectangleKeywords)!.Element(XmpNs.Rdf + "Bag");

    Assert.IsNotNull(bag);
  }

  [TestMethod]
  public void SetXmpArray_ReplacesExistingValues() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpArray(_rectangleKeywords, ["one", "two"]);

    element.SetXmpArray(_rectangleKeywords, ["three"]);

    CollectionAssert.AreEqual(new[] { "three" }, element.GetXmpStringArray(_rectangleKeywords)!.ToArray());
  }

  [TestMethod]
  public void SetXmpArray_Null_RemovesArray() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpArray(_rectangleKeywords, ["one", "two"]);

    element.SetXmpArray(_rectangleKeywords, null);

    Assert.IsNull(element.GetXmpPropertyElement(_rectangleKeywords));
  }

  [TestMethod]
  public void SetXmpArray_Empty_RemovesArray() {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetXmpArray(_rectangleKeywords, ["one", "two"]);

    element.SetXmpArray(_rectangleKeywords, []);

    Assert.IsNull(element.GetXmpPropertyElement(_rectangleKeywords));
  }

  [TestMethod]
  public void SetXmpArray_CreatesDescriptionAndPreservesExistingAttributes() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XAttribute(XmpNs.MpReg + "Rectangle", "0,0,1,1"));

    element.SetXmpArray(_rectangleKeywords, ["keyword1", "keyword2"]);

    var description = element.Element(XmpNs.Rdf + "Description");

    Assert.IsNotNull(description);

    // The existing properties were moved to the Description.
    Assert.AreEqual("Martin", (string?)description!.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.AreEqual("0,0,1,1", (string?)description.Attribute(XmpNs.MpReg + "Rectangle"));

    // They are no longer attributes of rdf:li.
    Assert.IsNull(element.Attribute(XmpNs.MpReg + "PersonDisplayName"));

    Assert.IsNull(element.Attribute(XmpNs.MpReg + "Rectangle"));

    // They weren't accidentally converted to elements either.
    Assert.AreEqual(0, description.Elements(XmpNs.MpReg + "PersonDisplayName").Count());

    Assert.AreEqual(0, description.Elements(XmpNs.MpReg + "Rectangle").Count());

    // RectangleKeywords was added exactly once.
    var keywords = description.Elements(_rectangleKeywords).Single();

    var bag = keywords.Element(XmpNs.Rdf + "Bag");

    Assert.IsNotNull(bag);

    CollectionAssert.AreEqual(
      new[] { "keyword1", "keyword2" },
      bag!.Elements(XmpNs.Rdf + "li").Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetAvailablePrefix_KnownNamespace_ReturnsPreferredPrefix() {
    var element = new XElement("root");

    Assert.AreEqual("MicrosoftPhoto", element.GetAvailablePrefix(XmpNs.MicrosoftPhoto));
  }

  [TestMethod]
  public void GetAvailablePrefix_CustomNamespace_ReturnsGeneratedPrefix() {
    var element = new XElement("root");
    XNamespace customNs = "MyCustomNamespace";

    Assert.AreEqual("ns1", element.GetAvailablePrefix(customNs));
  }

  [TestMethod]
  public void GetAvailablePrefix_CustomNamespace_WhenNs1IsUsed_ReturnsNextPrefix() {
    var element = new XElement("root",
      new XAttribute(XNamespace.Xmlns + "ns1", "SomeOtherNamespace"));

    XNamespace customNs = "MyCustomNamespace";

    Assert.AreEqual("ns2", element.GetAvailablePrefix(customNs));
  }
}