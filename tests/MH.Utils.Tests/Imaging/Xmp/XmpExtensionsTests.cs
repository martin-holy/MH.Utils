using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Tests.Imaging.Xmp;

[TestClass]
public class XmpExtensionsTests {
  [TestMethod]
  public void GetXmpProperty_ReadsAttribute() {
    var element = new XElement(XmpNs.Rdf + "li",
      new XAttribute(XmpNs.MpReg + "PersonDisplayName","Martin"));

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
}