using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class MpRegionCollection : IReadOnlyList<MpRegion> {
  private readonly XmpDocument _doc;
  private readonly XElement _bag;

  internal MpRegionCollection(XmpDocument doc) {
    _doc = doc;
    _bag = _getOrCreateBag();
  }

  public int Count =>
    _bag.Elements(XmpNs.Rdf + "li").Count();

  public MpRegion this[int index] =>
    new(_bag.Elements(XmpNs.Rdf + "li").ElementAt(index));

  public IEnumerator<MpRegion> GetEnumerator() =>
    _bag.Elements(XmpNs.Rdf + "li")
      .Select(e => new MpRegion(e))
      .GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() =>
    GetEnumerator();

  public MpRegion Add(string name, string? rectangle = null) {
    var element = new XElement(XmpNs.Rdf + "li");

    element.SetAttributeValue(XmpNs.MpReg + "PersonDisplayName", name);

    if (rectangle != null)
      element.SetAttributeValue(XmpNs.MpReg + "Rectangle", rectangle);

    _bag.Add(element);

    var person = new MpRegion(element);

    return person;
  }

  public void Remove(MpRegion person) {
    person.Element.Remove();
    _removeIfEmpty();
  }

  public void Clear() {
    _bag.RemoveNodes();
    _removeIfEmpty();
  }

  private XElement _getOrCreateBag() {
    var desc = _doc.GetOrCreateDescription(XmpNs.Mp);

    if (desc.Element(XmpNs.Mp + "RegionInfo") is not { } regionInfo) {
      regionInfo = new XElement(XmpNs.Mp + "RegionInfo");
      desc.Add(regionInfo);
    }

    if (regionInfo.Element(XmpNs.MpRi + "Regions") is not { } regions) {
      regions = new XElement(XmpNs.MpRi + "Regions");
      regionInfo.Add(regions);
    }

    if (regions.Element(XmpNs.Rdf + "Bag") is not { } bag) {
      bag = new XElement(XmpNs.Rdf + "Bag");
      regions.Add(bag);
    }

    return bag;
  }

  private void _removeIfEmpty() {
    if (_bag.HasElements) return;

    var regions = _bag.Parent;
    var regionInfo = regions?.Parent;

    _bag.Remove();

    if (regions?.HasElements == false)
      regions.Remove();

    if (regionInfo?.HasElements == false)
      regionInfo.Remove();

    _doc.RemoveEmptyDescriptions();
  }
}