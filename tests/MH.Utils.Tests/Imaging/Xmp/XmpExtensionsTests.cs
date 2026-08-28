using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging.Xmp;

[TestClass]
public class XmpExtensionsTests {
  private static XElement _createRdf(params XElement[] descriptions) =>
    new(XmpNs.Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", XmpNs.Rdf.NamespaceName), descriptions);

  private static XElement _createDescription(params object[] content) =>
    new(XmpNs.Rdf + "Description", new XAttribute(XmpNs.Rdf + "about", ""), content);

  [TestMethod]
  public void HasXmpProperty_Attribute_ReturnsTrue() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(name, "5"));

    Assert.IsTrue(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void HasXmpProperty_Element_ReturnsTrue() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XElement(name, "5"));

    Assert.IsTrue(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void HasXmpProperty_Missing_ReturnsFalse() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test");

    Assert.IsFalse(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void HasXmpProperty_PropertyOnDescendant_ReturnsFalse() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XElement("child", new XElement(name, "5")));

    Assert.IsFalse(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void HasXmpProperty_DifferentProperty_ReturnsFalse() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(XmpNs.Xmp + "Label", "test"));

    Assert.IsFalse(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void HasXmpProperty_CustomNamespace_ReturnsTrue() {
    var name = XNamespace.Get("urn:custom") + "Property";
    var element = new XElement("test", new XElement(name, "value"));

    Assert.IsTrue(element.HasXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyOnElement_ReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(name, "5"));

    Assert.AreSame(element, element.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyInDescription_ReturnsDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    Assert.AreSame(description, rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyInDescriptionElement_ReturnsDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(name, "5"));
    var rdf = _createRdf(description);

    Assert.AreSame(description, rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyInSecondDescription_ReturnsSecondDescription() {
    var name = XmpNs.Xmp + "Rating";
    var firstDescription = _createDescription(new XElement(XmpNs.Dc + "Title", "Title"));
    var secondDescription = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(firstDescription, secondDescription);

    Assert.AreSame(secondDescription, rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyInNestedDescription_ReturnsDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(XmpNs.Rdf + "li", _createDescription(new XAttribute(name, "5"))));
    var rdf = _createRdf(description);

    var nestedDescription = description
      .Element(XmpNs.Rdf + "li")!
      .Element(XmpNs.Rdf + "Description")!;

    Assert.AreSame(nestedDescription, rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyOnElementAndDescription_ReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    rdf.SetAttributeValue(name, "10");

    Assert.AreSame(rdf, rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyOnArbitraryDescendant_ReturnsNull() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription(new XElement("someElement", new XElement(name, "5"))));

    Assert.IsNull(rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpPropertyParent_PropertyMissing_ReturnsNull() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpPropertyParent(name));
  }

  [TestMethod]
  public void GetXmpProperty_Attribute_ReturnsValue() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(name, "5"));

    Assert.AreEqual("5", element.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_Element_ReturnsValue() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XElement(name, "5"));

    Assert.AreEqual("5", element.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_DescriptionAttribute_ReturnsValue() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    Assert.AreEqual("5", rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_DescriptionElement_ReturnsValue() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(name, "5"));
    var rdf = _createRdf(description);

    Assert.AreEqual("5", rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_MultipleDescriptions_ReturnsValueFromMatchingDescription() {
    var name = XmpNs.Xmp + "Rating";
    var firstDescription = _createDescription(new XElement(XmpNs.Dc + "Title", "Title"));
    var secondDescription = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(firstDescription, secondDescription);

    Assert.AreEqual("5", rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_CustomNamespace_ReturnsValue() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Property";
    var rdf = _createRdf(_createDescription(new XElement(name, "value")));

    Assert.AreEqual("value", rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_Missing_ReturnsNull() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_PropertyOnElementTakesPrecedenceOverDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    rdf.SetAttributeValue(name, "10");

    Assert.AreEqual("10", rdf.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpProperty_AttributeTakesPrecedenceOverElement() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(name, "attribute"), new XElement(name, "element"));

    Assert.AreEqual("attribute", element.GetXmpProperty(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_Element_ReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var property = new XElement(name, "5");
    var element = new XElement("test", property);

    Assert.AreSame(property, element.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_ElementInDescription_ReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var property = new XElement(name, "5");
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    Assert.AreSame(property, rdf.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_ElementInSecondDescription_ReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var property = new XElement(name, "5");
    var firstDescription = _createDescription(new XElement(XmpNs.Dc + "Title", "Title"));
    var secondDescription = _createDescription(property);
    var rdf = _createRdf(firstDescription, secondDescription);

    Assert.AreSame(property, rdf.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_Attribute_ReturnsNull() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement("test", new XAttribute(name, "5"));

    Assert.IsNull(element.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_Missing_ReturnsNull() {
    var name = XmpNs.Xmp + "Rating";
    var element = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(element.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpPropertyElement_CustomNamespace_ReturnsElement() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Property";
    var property = new XElement(name, "value");
    var rdf = _createRdf(_createDescription(property));

    Assert.AreSame(property, rdf.GetXmpPropertyElement(name));
  }

  [TestMethod]
  public void GetXmpDescription_Description_ReturnsItself() {
    var description = _createDescription(new XAttribute(XmpNs.Xmp + "Rating", "5"));

    Assert.AreSame(description, description.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_ReturnsDescriptionContainingNamespace() {
    var firstDescription = _createDescription(new XElement(XmpNs.Dc + "Title", "Title"));
    var secondDescription = _createDescription(new XElement(XmpNs.Xmp + "Rating", "5"));
    var rdf = _createRdf(firstDescription, secondDescription);

    Assert.AreSame(secondDescription, rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_FindsNamespaceInAttribute() {
    var description = _createDescription(new XAttribute(XmpNs.Xmp + "Rating", "5"));
    var rdf = _createRdf(description);

    Assert.AreSame(description, rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_FindsNamespaceInElement() {
    var description = _createDescription(new XElement(XmpNs.Xmp + "Rating", "5"));
    var rdf = _createRdf(description);

    Assert.AreSame(description, rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_WhenNamespaceIsMissing_ReturnsNull() {
    var rdf = _createRdf(_createDescription( new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_WithNoDescriptions_ReturnsNull() {
    var rdf = _createRdf();

    Assert.IsNull(rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Element_ReturnsDirectDescription() {
    var description = _createDescription(new XElement(XmpNs.Xmp + "Rating", "5"));
    var element = new XElement("test", description);

    Assert.AreSame(description, element.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Element_WithNoDirectDescription_ReturnsNull() {
    var element = new XElement("test",
      new XElement("child",
        _createDescription(new XElement(XmpNs.Xmp + "Rating", "5"))));

    Assert.IsNull(element.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpDescription_Rdf_NamespaceDeclarationAloneDoesNotMatch() {
    var description = _createDescription();
    description.Add(new XAttribute(XNamespace.Xmlns + "xmp", XmpNs.Xmp.NamespaceName));
    var rdf = _createRdf(description);

    Assert.IsNull(rdf.GetXmpDescription(XmpNs.Xmp));
  }

  [TestMethod]
  public void GetXmpArray_Bag_ReturnsItems() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one"),
        new XElement(XmpNs.Rdf + "li", "two")));

    var rdf = _createRdf(_createDescription(property));

    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "one", "two" }, result.Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetXmpArray_Seq_ReturnsItems() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Seq",
        new XElement(XmpNs.Rdf + "li", "one"),
        new XElement(XmpNs.Rdf + "li", "two")));

    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "one", "two" }, result.Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetXmpArray_Alt_ReturnsItems() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li", "English"),
        new XElement(XmpNs.Rdf + "li", "Czech")));

    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "English", "Czech" }, result.Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetXmpArray_CustomNamespace_ReturnsItems() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Keywords";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one"),
        new XElement(XmpNs.Rdf + "li", "two")));

    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "one", "two" }, result.Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetXmpArray_PropertyAsAttribute_ReturnsNull() {
    var name = XmpNs.Dc + "Subject";
    var rdf = _createRdf(_createDescription(new XAttribute(name, "one")));

    Assert.IsNull(rdf.GetXmpArray(name));
  }

  [TestMethod]
  public void GetXmpArray_PropertyWithoutArray_ReturnsNull() {
    var name = XmpNs.Dc + "Subject";
    var rdf = _createRdf(_createDescription(new XElement(name, "one")));

    Assert.IsNull(rdf.GetXmpArray(name));
  }

  [TestMethod]
  public void GetXmpArray_MissingProperty_ReturnsNull() {
    var name = XmpNs.Dc + "Subject";
    var rdf = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpArray(name));
  }

  [TestMethod]
  public void GetXmpArray_EmptyArray_ReturnsEmpty() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name, new XElement(XmpNs.Rdf + "Bag"));
    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    Assert.AreEqual(0, result.Count());
  }

  [TestMethod]
  public void GetXmpArray_ArrayWithOtherElements_ReturnsArrayItems() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Description"),
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one"),
        new XElement(XmpNs.Rdf + "li", "two")));

    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "one", "two" }, result.Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void GetXmpStringArray_ReturnsValues() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one"),
        new XElement(XmpNs.Rdf + "li", "two")));

    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpStringArray(name);

    Assert.IsNotNull(result);
    CollectionAssert.AreEqual(new[] { "one", "two" }, result.ToArray());
  }

  [TestMethod]
  public void GetXmpStringArray_EmptyArray_ReturnsEmpty() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name, new XElement(XmpNs.Rdf + "Bag"));
    var rdf = _createRdf(_createDescription(property));
    var result = rdf.GetXmpStringArray(name);

    Assert.IsNotNull(result);
    Assert.AreEqual(0, result.Count());
  }

  [TestMethod]
  public void GetXmpStringArray_MissingProperty_ReturnsNull() {
    var name = XmpNs.Dc + "Subject";
    var rdf = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpStringArray(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_XDefault_ReturnsXDefault() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "en"), "English"),
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "x-default"), "Default"),
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "cs"), "Czech")));

    var rdf = _createRdf(_createDescription(property));

    Assert.AreEqual("Default", rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_NoXDefault_ReturnsFirstTranslation() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "en"), "English"),
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "cs"), "Czech")));

    var rdf = _createRdf(_createDescription(property));

    Assert.AreEqual("English", rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_EmptyAlt_ReturnsNull() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name, new XElement(XmpNs.Rdf + "Alt"));
    var rdf = _createRdf(_createDescription(property));

    Assert.IsNull(rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_MissingAlt_ReturnsNull() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name, "Title");
    var rdf = _createRdf(_createDescription(property));

    Assert.IsNull(rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_MissingProperty_ReturnsNull() {
    var name = XmpNs.Xmp + "Title";
    var rdf = _createRdf(_createDescription(new XElement(XmpNs.Dc + "Title", "Title")));

    Assert.IsNull(rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_CustomNamespace_ReturnsValue() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "en"), "Custom title")));

    var rdf = _createRdf(_createDescription(property));

    Assert.AreEqual("Custom title", rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_AttributeProperty_ReturnsNull() {
    var name = XmpNs.Xmp + "Title";
    var rdf = _createRdf(_createDescription(new XAttribute(name, "Title")));

    Assert.IsNull(rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void GetXmpLangAlt_LiWithoutLanguage_ReturnsItsValue() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li", "Title")));

    var rdf = _createRdf(_createDescription(property));

    Assert.AreEqual("Title", rdf.GetXmpLangAlt(name));
  }

  [TestMethod]
  public void SetXmpProperty_NewProperty_DefaultsToAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription());

    rdf.SetXmpProperty(name, "5");

    Assert.AreEqual("5", (string?)rdf.Element(XmpNs.Rdf + "Description")!.Attribute(name));
    Assert.IsNull(rdf.Element(XmpNs.Rdf + "Description")!.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_NewProperty_AttributeStyle_CreatesAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription());

    rdf.SetXmpProperty(name, "5", XmpValueStyle.Attribute);

    var description = rdf.Element(XmpNs.Rdf + "Description")!;

    Assert.AreEqual("5", (string?)description.Attribute(name));
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_NewProperty_ElementStyle_CreatesElement() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription());

    rdf.SetXmpProperty(name, "5", XmpValueStyle.Element);

    var description = rdf.Element(XmpNs.Rdf + "Description")!;

    Assert.IsNull(description.Attribute(name));
    Assert.AreEqual("5", (string?)description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_Auto_PreservesAttributeStyle() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "3"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, "5");

    Assert.AreEqual("5", (string?)description.Attribute(name));
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_Auto_PreservesElementStyle() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(name, "3"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, "5");

    Assert.IsNull(description.Attribute(name));
    Assert.AreEqual("5", (string?)description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_ElementStyle_ChangesAttributeToElement() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "3"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, "5", XmpValueStyle.Element);

    Assert.IsNull(description.Attribute(name));
    Assert.AreEqual("5", (string?)description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_AttributeStyle_ChangesElementToAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(name, "3"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, "5", XmpValueStyle.Attribute);

    Assert.AreEqual("5", (string?)description.Attribute(name));
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_Null_RemovesProperty() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, null);

    Assert.IsNull(description.Attribute(name));
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpProperty_Null_RemovesEmptyDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "5"));
    var rdf = _createRdf(description);

    rdf.SetXmpProperty(name, null);

    Assert.IsNull(rdf.Element(XmpNs.Rdf + "Description"));
  }

  [TestMethod]
  public void SetXmpProperty_InvalidStyle_Throws() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription());

    Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
      rdf.SetXmpProperty(name, "5", (XmpValueStyle)999));
  }

  [TestMethod]
  public void SetXmpArray_CreatesBag() {
    var name = XmpNs.Dc + "Subject";
    var description = _createDescription();
    var rdf = _createRdf(description);

    var property = rdf.SetXmpArray(name, ["one", "two"]);

    Assert.IsNotNull(property);
    Assert.AreSame(property, description.Element(name));

    var array = property.Element(XmpNs.Rdf + "Bag");

    Assert.IsNotNull(array);
    CollectionAssert.AreEqual(
      new[] { "one", "two" },
      array.Elements(XmpNs.Rdf + "li").Select(x => x.Value).ToArray());
  }

  [TestMethod]
  public void SetXmpArray_CreatesSpecifiedArrayType() {
    var name = XmpNs.Dc + "Subject";
    var description = _createDescription();
    var rdf = _createRdf(description);

    var property = rdf.SetXmpArray(name, ["one"], XmpArrayType.Seq);

    Assert.IsNotNull(property);
    Assert.IsNotNull(property.Element(XmpNs.Rdf + "Seq"));
    Assert.IsNull(property.Element(XmpNs.Rdf + "Bag"));
  }

  [TestMethod]
  public void SetXmpArray_ReplacesExistingArray() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "old")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    var result = rdf.SetXmpArray(name, ["new1", "new2"]);

    Assert.AreSame(property, result);
    CollectionAssert.AreEqual(
      new[] { "new1", "new2" },
      property
        .Element(XmpNs.Rdf + "Bag")!
        .Elements(XmpNs.Rdf + "li")
        .Select(x => x.Value)
        .ToArray());
  }

  [TestMethod]
  public void SetXmpArray_ChangesArrayType() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "old")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    rdf.SetXmpArray(name, ["new"], XmpArrayType.Seq);

    Assert.IsNull(property.Element(XmpNs.Rdf + "Bag"));
    Assert.IsNotNull(property.Element(XmpNs.Rdf + "Seq"));
  }

  [TestMethod]
  public void SetXmpArray_FiltersEmptyValues() {
    var name = XmpNs.Dc + "Subject";
    var rdf = _createRdf(_createDescription());

    var property = rdf.SetXmpArray(name, ["one", "", "  ", "two"]);

    Assert.IsNotNull(property);

    CollectionAssert.AreEqual(
      new[] { "one", "two" },
      property
        .Element(XmpNs.Rdf + "Bag")!
        .Elements(XmpNs.Rdf + "li")
        .Select(x => x.Value)
        .ToArray());
  }

  [TestMethod]
  public void SetXmpArray_Null_RemovesProperty() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    var result = rdf.SetXmpArray(name, null);

    Assert.IsNull(result);
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpArray_EmptyValues_RemovesProperty() {
    var name = XmpNs.Dc + "Subject";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "one")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    var result = rdf.SetXmpArray(name, []);

    Assert.IsNull(result);
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpArray_EmptyValues_RemovesEmptyDescription() {
    var name = XmpNs.Dc + "Subject";
    var description = _createDescription(
      new XElement(name,
        new XElement(XmpNs.Rdf + "Bag",
          new XElement(XmpNs.Rdf + "li", "one"))));
    var rdf = _createRdf(description);

    rdf.SetXmpArray(name, []);

    Assert.IsNull(rdf.Element(XmpNs.Rdf + "Description"));
  }

  [TestMethod]
  public void SetXmpArray_PreservesOtherProperties() {
    var name = XmpNs.Dc + "Subject";
    var otherName = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(otherName, "5"));
    var rdf = _createRdf(description);

    rdf.SetXmpArray(name, ["one"]);

    Assert.AreEqual("5", (string?)description.Attribute(otherName));
    Assert.IsNotNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpLangAlt_CreatesPropertyAndDefaultLanguage() {
    var name = XmpNs.Xmp + "Title";
    var description = _createDescription();
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, "Hello");

    var property = description.Element(name);

    Assert.IsNotNull(property);

    var item = property
      .Element(XmpNs.Rdf + "Alt")!
      .Element(XmpNs.Rdf + "li");

    Assert.IsNotNull(item);
    Assert.AreEqual("x-default", (string?)item.Attribute(XNamespace.Xml + "lang"));
    Assert.AreEqual("Hello", item.Value);
  }

  [TestMethod]
  public void SetXmpLangAlt_UpdatesExistingDefaultLanguage() {
    var name = XmpNs.Xmp + "Title";
    var item = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XNamespace.Xml + "lang", "x-default"), "Old");
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt", item));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, "New");

    Assert.AreEqual("New", item.Value);
    Assert.AreEqual(1, property.Element(XmpNs.Rdf + "Alt")!.Elements(XmpNs.Rdf + "li").Count());
  }

  [TestMethod]
  public void SetXmpLangAlt_CreatesDefaultAlongsideExistingTranslations() {
    var name = XmpNs.Xmp + "Title";
    var english = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XNamespace.Xml + "lang", "en"), "English");
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt", english));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, "Default");

    var items = property
      .Element(XmpNs.Rdf + "Alt")!
      .Elements(XmpNs.Rdf + "li")
      .ToArray();

    Assert.AreEqual(2, items.Length);
    Assert.AreSame(english, items[0]);
    Assert.AreEqual("x-default", (string?)items[1].Attribute(XNamespace.Xml + "lang"));
    Assert.AreEqual("Default", items[1].Value);
  }

  [TestMethod]
  public void SetXmpLangAlt_RemovesAttributeRepresentation() {
    var name = XmpNs.Xmp + "Title";
    var description = _createDescription(new XAttribute(name, "Old"));
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, "New");

    Assert.IsNull(description.Attribute(name));
    Assert.AreEqual("New", description.Element(name)!
      .Element(XmpNs.Rdf + "Alt")!
      .Element(XmpNs.Rdf + "li")!
      .Value);
  }

  [TestMethod]
  public void SetXmpLangAlt_Null_RemovesProperty() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "x-default"), "Old")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, null);

    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpLangAlt_Null_PreservesOtherProperties() {
    var name = XmpNs.Xmp + "Title";
    var otherName = XmpNs.Xmp + "Rating";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Alt",
        new XElement(XmpNs.Rdf + "li",
          new XAttribute(XNamespace.Xml + "lang", "x-default"), "Old")));
    var description = _createDescription(property, new XAttribute(otherName, "5"));
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, null);

    Assert.IsNull(description.Element(name));
    Assert.AreEqual("5", (string?)description.Attribute(otherName));
  }

  [TestMethod]
  public void SetXmpLangAlt_ReplacesInvalidArrayStructure() {
    var name = XmpNs.Xmp + "Title";
    var property = new XElement(name,
      new XElement(XmpNs.Rdf + "Bag",
        new XElement(XmpNs.Rdf + "li", "Old")));
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    rdf.SetXmpLangAlt(name, "New");

    var alt = property.Element(XmpNs.Rdf + "Alt");

    Assert.IsNotNull(alt);
    Assert.IsNull(property.Element(XmpNs.Rdf + "Bag"));
    Assert.AreEqual("New", alt.Element(XmpNs.Rdf + "li")!.Value);
  }

  [TestMethod]
  public void SetXmpAttribute_ExistingAttribute_UpdatesValue() {
    var name = XmpNs.Xmp + "Rating";
    var attribute = new XAttribute(name, "3");
    var parent = _createDescription(attribute);

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", attribute.Value);
    Assert.IsNull(parent.Element(name));
  }

  [TestMethod]
  public void SetXmpAttribute_ExistingElement_ConvertsToAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription(new XElement(name, "3"));

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", (string?)parent.Attribute(name));
    Assert.IsNull(parent.Element(name));
  }

  [TestMethod]
  public void SetXmpAttribute_ExistingSimpleElement_ConvertsToAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription(new XElement(name, "3"));

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", (string?)parent.Attribute(name));
    Assert.IsNull(parent.Element(name));
  }

  [TestMethod]
  public void SetXmpAttribute_ElementWithAttributes_UpdatesElementInstead() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription(
      new XElement(name,
        new XAttribute(XNamespace.Xml + "lang", "en"), "3"));

    parent.SetXmpAttribute(name, "5");

    var element = parent.Element(name);

    Assert.IsNotNull(element);
    Assert.AreEqual("5", element.Value);
    Assert.IsNull(parent.Attribute(name));
  }

  [TestMethod]
  public void SetXmpAttribute_NewProperty_CreatesAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", (string?)parent.Attribute(name));
    Assert.IsNull(parent.Element(name));
  }

  [TestMethod]
  public void SetXmpAttribute_NewPropertyWithDescription_AddsToDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription();
    var parent = new XElement("resource", description);

    parent.SetXmpAttribute(name, "5");

    Assert.IsNull(parent.Attribute(name));
    Assert.AreEqual("5", (string?)description.Attribute(name));
  }

  [TestMethod]
  public void SetXmpAttribute_AttributeInDescription_UpdatesAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var attribute = new XAttribute(name, "3");
    var description = _createDescription(attribute);
    var parent = new XElement("resource", description);

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", attribute.Value);
    Assert.IsNull(parent.Attribute(name));
    Assert.IsNull(parent.Element(name));
    Assert.AreEqual(1, description.Attributes(name).Count());
  }

  [TestMethod]
  public void SetXmpAttribute_ElementInDescription_ConvertsToAttribute() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XElement(name, "3"));
    var parent = new XElement("resource", description);

    parent.SetXmpAttribute(name, "5");

    Assert.AreEqual("5", (string?)description.Attribute(name));
    Assert.IsNull(description.Element(name));
  }

  [TestMethod]
  public void SetXmpAttribute_ElementWithAttributesInDescription_UpdatesElement() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(
      new XElement(name,
        new XAttribute(XNamespace.Xml + "lang", "en"), "3"));
    var parent = new XElement("resource", description);

    parent.SetXmpAttribute(name, "5");

    var element = description.Element(name);

    Assert.IsNotNull(element);
    Assert.AreEqual("5", element.Value);
    Assert.IsNull(description.Attribute(name));
  }

  [TestMethod]
  public void SetXmpElement_ExistingElement_UpdatesValue() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement(name, "3");
    var parent = _createDescription(element);

    parent.SetXmpElement(name, "5");

    Assert.AreSame(element, parent.Element(name));
    Assert.AreEqual("5", element.Value);
    Assert.IsNull(parent.Attribute(name));
  }

  [TestMethod]
  public void SetXmpElement_ExistingAttribute_ConvertsToElement() {
    var name = XmpNs.Xmp + "Rating";
    var attribute = new XAttribute(name, "3");
    var parent = _createDescription(attribute);

    parent.SetXmpElement(name, "5");

    Assert.IsNull(parent.Attribute(name));
    Assert.AreEqual("5", (string?)parent.Element(name));
  }

  [TestMethod]
  public void SetXmpElement_ElementWithAttributes_PreservesAttributes() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement(name, new XAttribute(XNamespace.Xml + "lang", "en"), "3");
    var parent = _createDescription(element);

    parent.SetXmpElement(name, "5");

    Assert.AreSame(element, parent.Element(name));
    Assert.AreEqual("5", element.Value);
    Assert.AreEqual("en", (string?)element.Attribute(XNamespace.Xml + "lang"));
  }

  [TestMethod]
  public void SetXmpElement_NewProperty_CreatesElement() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    parent.SetXmpElement(name, "5");

    Assert.IsNull(parent.Attribute(name));
    Assert.AreEqual("5", (string?)parent.Element(name));
  }

  [TestMethod]
  public void SetXmpElement_AttributeInDescription_ConvertsToElement() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription(new XAttribute(name, "3"));
    var parent = new XElement("resource", description);

    parent.SetXmpElement(name, "5");

    Assert.IsNull(description.Attribute(name));
    Assert.AreEqual("5", (string?)description.Element(name));
  }

  [TestMethod]
  public void SetXmpElement_ElementInDescription_UpdatesElement() {
    var name = XmpNs.Xmp + "Rating";
    var element = new XElement(name, "3");
    var description = _createDescription(element);
    var parent = new XElement("resource", description);

    parent.SetXmpElement(name, "5");

    Assert.AreSame(element, description.Element(name));
    Assert.AreEqual("5", element.Value);
  }

  [TestMethod]
  public void SetXmpElement_AttributeWithDescription_CreatesElementInDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription();
    var parent = new XElement("resource", new XAttribute(name, "3"), description);

    parent.SetXmpElement(name, "5");

    Assert.IsNull(parent.Attribute(name));
    Assert.AreEqual("5", (string?)description.Element(name));
  }

  [TestMethod]
  public void SetXmpElement_PreservesOtherProperties() {
    var name = XmpNs.Xmp + "Rating";
    var otherName = XmpNs.Xmp + "Label";
    var parent = _createDescription(new XAttribute(otherName, "important"));

    parent.SetXmpElement(name, "5");

    Assert.AreEqual("important", (string?)parent.Attribute(otherName));
    Assert.AreEqual("5", (string?)parent.Element(name));
  }

  [TestMethod]
  public void GetOrCreateXmpPropertyElement_ExistingElement_ReturnsSameElement() {
    var name = XmpNs.Xmp + "Rating";
    var property = new XElement(name, "5");
    var description = _createDescription(property);
    var rdf = _createRdf(description);

    var result = rdf.GetOrCreateXmpPropertyElement(name);

    Assert.AreSame(property, result);
    Assert.AreEqual(1, description.Elements(name).Count());
  }

  [TestMethod]
  public void GetOrCreateXmpPropertyElement_MissingProperty_CreatesElementInDescription() {
    var name = XmpNs.Xmp + "Rating";
    var description = _createDescription();
    var rdf = _createRdf(description);

    var result = rdf.GetOrCreateXmpPropertyElement(name);

    Assert.AreSame(description, result.Parent);
    Assert.AreEqual(name, result.Name);
    Assert.AreEqual(1, description.Elements(name).Count());
    Assert.AreEqual(0, result.Nodes().Count());
  }

  [TestMethod]
  public void GetOrCreateXmpPropertyElement_MissingDescription_CreatesDescriptionAndElement() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf();

    var result = rdf.GetOrCreateXmpPropertyElement(name);
    var description = rdf.Element(XmpNs.Rdf + "Description");

    Assert.IsNotNull(description);
    Assert.AreSame(description, result.Parent);
    Assert.AreEqual(name, result.Name);
  }

  [TestMethod]
  public void GetOrCreateXmpPropertyElement_CalledTwice_DoesNotDuplicateElement() {
    var name = XmpNs.Xmp + "Rating";
    var rdf = _createRdf(_createDescription());

    var first = rdf.GetOrCreateXmpPropertyElement(name);
    var second = rdf.GetOrCreateXmpPropertyElement(name);

    Assert.AreSame(first, second);
    Assert.AreEqual(1, rdf
      .Elements(XmpNs.Rdf + "Description")
      .SelectMany(d => d.Elements(name))
      .Count());
  }

  [TestMethod]
  public void GetOrCreateXmpPropertyElement_CustomNamespace_CreatesElementInDescription() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Property";
    var rdf = _createRdf(_createDescription());

    var result = rdf.GetOrCreateXmpPropertyElement(name);

    Assert.AreSame(rdf.Element(XmpNs.Rdf + "Description"), result.Parent);
    Assert.AreEqual(name, result.Name);
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_ExistingDescription_ReturnsSameDescription() {
    var description = _createDescription();
    var rdf = _createRdf(description);

    var result = rdf.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(description, result);
    Assert.AreEqual(1, rdf.Elements(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Rdf_CreatesDescription() {
    var rdf = _createRdf();

    var result = rdf.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(rdf, result.Parent);
    Assert.AreEqual(XmpNs.Rdf + "Description", result.Name);
    Assert.AreEqual("", (string?)result.Attribute(XmpNs.Rdf + "about"));
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Rdf_ReusesExistingDescriptionForDifferentNamespace() {
    var description = _createDescription(new XElement(XmpNs.Dc + "Title", "Title"));
    var rdf = _createRdf(description);

    var result = rdf.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(description, result);
    Assert.AreEqual(1, rdf.Elements(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Resource_CreatesDescription() {
    var resource = new XElement(XmpNs.Rdf + "li");

    var result = resource.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(resource, result.Parent);
    Assert.AreEqual(XmpNs.Rdf + "Description", result.Name);
    Assert.AreEqual("", (string?)result.Attribute(XmpNs.Rdf + "about"));
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Resource_MovesXmpAttributesToDescription() {
    var name = XmpNs.Xmp + "Rating";
    var resource = new XElement(XmpNs.Rdf + "li", new XAttribute(name, "5"));

    var result = resource.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.IsNull(resource.Attribute(name));
    Assert.AreEqual("5", (string?)result.Attribute(name));
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Resource_MovesXmpElementsToDescription() {
    var name = XmpNs.Xmp + "Rating";
    var property = new XElement(name, "5");
    var resource = new XElement(XmpNs.Rdf + "li", property);

    var result = resource.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.IsNull(resource.Element(name));
    Assert.AreSame(result.Element(name), result.Elements(name).Single());
    Assert.AreEqual("5", (string?)result.Element(name));
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_Resource_PreservesRdfElements() {
    var resource = new XElement(XmpNs.Rdf + "li", new XElement(XmpNs.Rdf + "Description"));

    var result = resource.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(result, resource.Element(XmpNs.Rdf + "Description"));
    Assert.AreEqual(1, resource.Elements(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void GetOrCreateXmpDescription_CalledTwice_DoesNotCreateDuplicate() {
    var rdf = _createRdf();

    var first = rdf.GetOrCreateXmpDescription(XmpNs.Xmp);
    var second = rdf.GetOrCreateXmpDescription(XmpNs.Xmp);

    Assert.AreSame(first, second);
    Assert.AreEqual(1, rdf.Elements(XmpNs.Rdf + "Description").Count());
  }

  [TestMethod]
  public void CreateXmpElement_CreatesAndReturnsElement() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    var result = parent.CreateXmpElement(name);

    Assert.AreSame(result, parent.Element(name));
    Assert.AreEqual(name, result.Name);
  }

  [TestMethod]
  public void CreateXmpElement_AddsElementToParent() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    var result = parent.CreateXmpElement(name);

    Assert.AreEqual(1, parent.Elements(name).Count());
    Assert.AreSame(parent, result.Parent);
  }

  [TestMethod]
  public void CreateXmpElement_ExistingNamespace_DoesNotAddNamespaceDeclaration() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();
    parent.Add(new XAttribute(XNamespace.Xmlns + "xmp", XmpNs.Xmp.NamespaceName));

    var namespaceDeclarationsBefore = parent.Attributes()
      .Count(a => a.IsNamespaceDeclaration);

    parent.CreateXmpElement(name);

    var namespaceDeclarationsAfter = parent.Attributes()
      .Count(a => a.IsNamespaceDeclaration);

    Assert.AreEqual(namespaceDeclarationsBefore, namespaceDeclarationsAfter);
  }

  [TestMethod]
  public void CreateXmpElement_CustomNamespace_AddsNamespaceDeclaration() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Property";
    var parent = _createDescription();

    var result = parent.CreateXmpElement(name);

    Assert.AreEqual(ns, result.Name.Namespace);
    Assert.AreEqual(ns.NamespaceName,
      parent.GetNamespaceOfPrefix(result.GetPrefixOfNamespace(ns)!));
  }

  [TestMethod]
  public void CreateXmpElement_DoesNotReuseExistingElement() {
    var name = XmpNs.Xmp + "Rating";
    var existing = new XElement(name, "old");
    var parent = _createDescription(existing);

    var result = parent.CreateXmpElement(name);

    Assert.AreNotSame(existing, result);
    Assert.AreEqual(2, parent.Elements(name).Count());
  }

  [TestMethod]
  public void GetOrCreateXmpElement_ExistingElement_ReturnsSameElement() {
    var name = XmpNs.Xmp + "Rating";
    var existing = new XElement(name, "5");
    var parent = _createDescription(existing);

    var result = parent.GetOrCreateXmpElement(name);

    Assert.AreSame(existing, result);
    Assert.AreEqual(1, parent.Elements(name).Count());
  }

  [TestMethod]
  public void GetOrCreateXmpElement_MissingElement_CreatesElement() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    var result = parent.GetOrCreateXmpElement(name);

    Assert.AreSame(result, parent.Element(name));
    Assert.AreEqual(name, result.Name);
    Assert.AreEqual(1, parent.Elements(name).Count());
  }

  [TestMethod]
  public void GetOrCreateXmpElement_CalledTwice_ReturnsSameElement() {
    var name = XmpNs.Xmp + "Rating";
    var parent = _createDescription();

    var first = parent.GetOrCreateXmpElement(name);
    var second = parent.GetOrCreateXmpElement(name);

    Assert.AreSame(first, second);
    Assert.AreEqual(1, parent.Elements(name).Count());
  }

  [TestMethod]
  public void GetOrCreateXmpElement_ExistingElement_DoesNotModifyValue() {
    var name = XmpNs.Xmp + "Rating";
    var existing = new XElement(name, "original");
    var parent = _createDescription(existing);

    var result = parent.GetOrCreateXmpElement(name);

    Assert.AreSame(existing, result);
    Assert.AreEqual("original", result.Value);
  }

  [TestMethod]
  public void GetOrCreateXmpElement_CustomNamespace_CreatesElement() {
    var ns = XNamespace.Get("urn:custom");
    var name = ns + "Property";
    var parent = _createDescription();

    var result = parent.GetOrCreateXmpElement(name);

    Assert.AreEqual(name, result.Name);
    Assert.AreEqual(1, parent.Elements(name).Count());
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_ExistingNamespace_DoesNothing() {
    var parent = _createDescription();
    parent.Add(new XAttribute(XNamespace.Xmlns + "xmp", XmpNs.Xmp.NamespaceName));

    var before = parent.Attributes().Count(a => a.IsNamespaceDeclaration);

    var result = parent.EnsureXmpNamespacePrefix(XmpNs.Xmp);

    Assert.AreSame(parent, result);
    Assert.AreEqual(before, parent.Attributes().Count(a => a.IsNamespaceDeclaration));
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_UsesPreferredPrefix() {
    var parent = _createDescription();

    parent.EnsureXmpNamespacePrefix(XmpNs.Xmp);

    Assert.AreEqual(XmpNs.Xmp.NamespaceName, parent.GetNamespaceOfPrefix("xmp")?.NamespaceName);
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_WhenPreferredPrefixIsTaken_UsesAvailablePrefix() {
    var parent = _createDescription();
    var otherNs = XNamespace.Get("urn:other");

    parent.Add(new XAttribute(XNamespace.Xmlns + "xmp", otherNs.NamespaceName));

    parent.EnsureXmpNamespacePrefix(XmpNs.Xmp);

    Assert.AreEqual(otherNs.NamespaceName, parent.GetNamespaceOfPrefix("xmp")?.NamespaceName);

    var prefix = parent.GetPrefixOfNamespace(XmpNs.Xmp);

    Assert.IsNotNull(prefix);
    Assert.AreNotEqual("xmp", prefix);
    Assert.AreEqual(XmpNs.Xmp.NamespaceName, parent.GetNamespaceOfPrefix(prefix!)?.NamespaceName);
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_ReturnsSameElement() {
    var parent = _createDescription();

    var result = parent.EnsureXmpNamespacePrefix(XmpNs.Xmp);

    Assert.AreSame(parent, result);
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_CreatesNamespaceForCustomNamespace() {
    var ns = XNamespace.Get("urn:custom");
    var parent = _createDescription();

    parent.EnsureXmpNamespacePrefix(ns);

    var prefix = parent.GetPrefixOfNamespace(ns);

    Assert.IsNotNull(prefix);
    Assert.AreEqual(ns.NamespaceName, parent.GetNamespaceOfPrefix(prefix!)?.NamespaceName);
  }

  [TestMethod]
  public void EnsureXmpNamespacePrefix_WhenPreferredPrefixIsTaken_UsesFirstAvailableGeneratedPrefix() {
    var parent = _createDescription();
    var ns = XNamespace.Get("urn:custom");

    parent.Add(new XAttribute(XNamespace.Xmlns + "ns1", "urn:other"));

    parent.EnsureXmpNamespacePrefix(ns);

    Assert.AreEqual(ns.NamespaceName, parent.GetNamespaceOfPrefix("ns2")?.NamespaceName);
  }
}