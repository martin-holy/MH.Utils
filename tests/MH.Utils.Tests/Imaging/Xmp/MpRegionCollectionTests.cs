using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging.Xmp;

[TestClass]
public class MpRegionCollectionTests {
  private static readonly XName _rectangleKeywords = XNamespace.Get("urn:martin-holy:xmp-test") + "RectangleKeywords";

  [TestMethod]
  public void MpRegionCollection_EmptyDocument_HasNoRegions() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    Assert.AreEqual(0, regions.Count);
  }

  [TestMethod]
  public void MpRegionCollection_Add_ReturnsRegionWithValues() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    Assert.AreEqual(1, regions.Count);
    Assert.AreEqual("Martin", region.PersonDisplayName);
    Assert.AreEqual("0,0,1,1", region.Rectangle);
  }

  [TestMethod]
  public void MpRegionCollection_Add_StoresValuesAsAttributes() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin", "0,0,1,1");

    var li = document.Document.Descendants(XmpNs.Rdf + "li").Single();

    Assert.AreEqual("Martin", (string?)li.Attribute(XmpNs.MpReg + "PersonDisplayName"));
    Assert.AreEqual("0,0,1,1", (string?)li.Attribute(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void MpRegionCollection_AddWithoutRectangle_LeavesRectangleNull() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin");

    Assert.AreEqual("Martin", region.PersonDisplayName);
    Assert.IsNull(region.Rectangle);
  }

  [TestMethod]
  public void MpRegionCollection_Indexer_ReturnsRegion() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin");
    regions.Add("Gee");

    Assert.AreEqual("Martin", regions[0].PersonDisplayName);
    Assert.AreEqual("Gee", regions[1].PersonDisplayName);
  }

  [TestMethod]
  public void MpRegionCollection_Enumeration_ReturnsAllRegions() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin");
    regions.Add("Gee");

    var names = regions.Select(r => r.PersonDisplayName).ToArray();

    CollectionAssert.AreEqual(new[] { "Martin", "Gee" }, names);
  }

  [TestMethod]
  public void MpRegionCollection_Remove_RemovesRegion() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    var first = regions.Add("Martin");
    regions.Add("Gee");

    regions.Remove(first);

    Assert.AreEqual(1, regions.Count);
    Assert.AreEqual("Gee", regions[0].PersonDisplayName);
  }

  [TestMethod]
  public void MpRegionCollection_Clear_RemovesAllRegions() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin");
    regions.Add("Gee");

    regions.Clear();

    Assert.AreEqual(0, regions.Count);
  }

  [TestMethod]
  public void MpRegionCollection_Clear_RemovesEmptyRegionStructure() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin");
    regions.Clear();

    Assert.IsFalse(document.Document.Descendants(XmpNs.Mp + "RegionInfo").Any());
    Assert.IsFalse(document.Document.Descendants(XmpNs.MpRi + "Regions").Any());
    Assert.IsFalse(document.Document.Descendants(XmpNs.Rdf + "Bag").Any());
  }

  [TestMethod]
  public void MpRegionCollection_RemoveLastRegion_RemovesEmptyRegionStructure() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    var region = regions.Add("Martin");
    regions.Remove(region);

    Assert.AreEqual(0, regions.Count);
    Assert.IsFalse(document.Document.Descendants(XmpNs.Mp + "RegionInfo").Any());
    Assert.IsFalse(document.Document.Descendants(XmpNs.MpRi + "Regions").Any());
  }

  [TestMethod]
  public void MpRegionCollection_RemoveOneRegion_PreservesRemainingStructure() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    var first = regions.Add("Martin");
    regions.Add("Gee");

    regions.Remove(first);

    Assert.IsNotNull(document.Document.Descendants(XmpNs.Mp + "RegionInfo").Single());
    Assert.IsNotNull(document.Document.Descendants(XmpNs.MpRi + "Regions").Single());
    Assert.AreEqual(1, document.Document.Descendants(XmpNs.Rdf + "li").Count());
  }

  [TestMethod]
  public void MpRegion_PersonDisplayName_CanBeChanged() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin");

    region.PersonDisplayName = "Martin2";

    Assert.AreEqual("Martin2", region.PersonDisplayName);

    var li = document.Document.Descendants(XmpNs.Rdf + "li").Single();
    Assert.AreEqual("Martin2", (string?)li.Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void MpRegion_Rectangle_CanBeChanged() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    region.Rectangle = "0.1,0.2,0.3,0.4";

    Assert.AreEqual("0.1,0.2,0.3,0.4", region.Rectangle);

    var li = document.Document.Descendants(XmpNs.Rdf + "li").Single();
    Assert.AreEqual("0.1,0.2,0.3,0.4", (string?)li.Attribute(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void MpRegion_PersonDisplayName_NullRemovesProperty() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin");

    region.PersonDisplayName = null;

    Assert.IsNull(region.PersonDisplayName);
    Assert.IsNull(document.Document.Descendants(XmpNs.Rdf + "li").Single().Attribute(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void MpRegion_Rectangle_NullRemovesProperty() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    region.Rectangle = null;

    Assert.IsNull(region.Rectangle);
    Assert.IsNull(document.Document.Descendants(XmpNs.Rdf + "li").Single().Attribute(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void MpRegion_PreservesExistingAttributesWhenDescriptionIsCreated() {
    var document = new XmpDocument(null);

    var rdf = document.Document.Descendants(XmpNs.Rdf + "RDF").Single();
    var li = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XAttribute(XmpNs.MpReg + "Rectangle", "0,0,1,1"));

    var bag = new XElement(XmpNs.Rdf + "Bag", li);
    var regionsElement = new XElement(XmpNs.MpRi + "Regions", bag);
    var regionInfo = new XElement(XmpNs.Mp + "RegionInfo", regionsElement);

    document.GetOrCreateDescription(XmpNs.Mp).Add(regionInfo);

    var regions = new MpRegionCollection(document);
    var region = regions[0];

    var keywords = new[] { "keyword1", "keyword2" };
    li.SetXmpArray(_rectangleKeywords, keywords);

    Assert.AreEqual("Martin", region.PersonDisplayName);
    Assert.AreEqual("0,0,1,1", region.Rectangle);

    var description = li.GetXmpDescription();
    Assert.IsNotNull(description);
    Assert.AreEqual("Martin", (string?)description!.Attribute(XmpNs.MpReg + "PersonDisplayName"));
    Assert.AreEqual("0,0,1,1", (string?)description.Attribute(XmpNs.MpReg + "Rectangle"));

    Assert.IsNull(li.Attribute(XmpNs.MpReg + "PersonDisplayName"));
    Assert.IsNull(li.Attribute(XmpNs.MpReg + "Rectangle"));
  }
}