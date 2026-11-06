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
  public void MpRegionCollection_Add_StoresValuesAsElements() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);

    regions.Add("Martin", "0,0,1,1");

    var liDesc = document.Document.Descendants(XmpNs.Rdf + "li").Single()
      .Element(XmpNs.Rdf + "Description");

    Assert.AreEqual("Martin", (string?)liDesc?.Element(XmpNs.MpReg + "PersonDisplayName"));
    Assert.AreEqual("0,0,1,1", (string?)liDesc?.Element(XmpNs.MpReg + "Rectangle"));

    var regionInfo = document.Document.Descendants(XmpNs.Mp + "RegionInfo").Single();
    var regionInfoDescription = regionInfo.Element(XmpNs.Rdf + "Description");
    Assert.IsNotNull(regionInfoDescription?.Element(XmpNs.MpRi + "Regions"));
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

    var liDesc = document.Document.Descendants(XmpNs.Rdf + "li").Single()
      .Element(XmpNs.Rdf + "Description");
    Assert.AreEqual("Martin2", (string?)liDesc?.Element(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void MpRegion_Rectangle_CanBeChanged() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    region.Rectangle = "0.1,0.2,0.3,0.4";

    Assert.AreEqual("0.1,0.2,0.3,0.4", region.Rectangle);

    var liDesc = document.Document.Descendants(XmpNs.Rdf + "li").Single()
      .Element(XmpNs.Rdf + "Description");
    Assert.AreEqual("0.1,0.2,0.3,0.4", (string?)liDesc?.Element(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void MpRegion_PersonDisplayName_NullRemovesProperty() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    region.PersonDisplayName = null;

    Assert.IsNull(region.PersonDisplayName);
    Assert.IsNull(document.Document.Descendants(XmpNs.Rdf + "li").Single()
      .Element(XmpNs.Rdf + "Description")!.Element(XmpNs.MpReg + "PersonDisplayName"));
  }

  [TestMethod]
  public void MpRegion_Rectangle_NullRemovesProperty() {
    var document = new XmpDocument(null);
    var regions = new MpRegionCollection(document);
    var region = regions.Add("Martin", "0,0,1,1");

    region.Rectangle = null;

    Assert.IsNull(region.Rectangle);
    Assert.IsNull(document.Document.Descendants(XmpNs.Rdf + "li").Single()
      .Element(XmpNs.Rdf + "Description")!.Element(XmpNs.MpReg + "Rectangle"));
  }

  [TestMethod]
  public void MpRegionCollection_Add_NormalizesExistingRegionWithoutDescription() {
    var document = new XmpDocument(null);

    var li = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName", "Martin"),
      new XElement(XmpNs.MpReg + "Rectangle", "0,0,1,1"),
      new XElement(_rectangleKeywords,
        new XElement(XmpNs.Rdf + "Bag",
          new XElement(XmpNs.Rdf + "li", "keyword1"),
          new XElement(XmpNs.Rdf + "li", "keyword2"))));

    var bag = new XElement(XmpNs.Rdf + "Bag", li);
    var regionsElement = new XElement(XmpNs.MpRi + "Regions", bag);
    var regionInfo = new XElement(XmpNs.Mp + "RegionInfo", regionsElement);

    document.Rdf.GetOrCreateXmpDescription(XmpNs.Mp).Add(regionInfo);

    var regions = new MpRegionCollection(document);
    regions.Add("New person");

    var description = li.Element(XmpNs.Rdf + "Description");
    Assert.IsNotNull(description);
    Assert.AreEqual("Martin", (string?)description!.Element(XmpNs.MpReg + "PersonDisplayName"));
    Assert.AreEqual("0,0,1,1", (string?)description.Element(XmpNs.MpReg + "Rectangle"));
    CollectionAssert.AreEqual(new[] { "keyword1", "keyword2" },
      description.GetXmpStringArray(_rectangleKeywords)!.ToArray());

    Assert.AreEqual("Martin", regions[0].PersonDisplayName);
    Assert.AreEqual("0,0,1,1", regions[0].Rectangle);

    Assert.IsNull(li.Attribute(XmpNs.MpReg + "PersonDisplayName"));
    Assert.IsNull(li.Attribute(XmpNs.MpReg + "Rectangle"));

    var regionInfoDescription = regionInfo.Element(XmpNs.Rdf + "Description");
    Assert.IsNotNull(regionInfoDescription?.Element(XmpNs.MpRi + "Regions"));
  }
}