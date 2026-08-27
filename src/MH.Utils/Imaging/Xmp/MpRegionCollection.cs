using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public sealed class MpRegionCollection(XmpDocument doc) : IReadOnlyList<MpRegion> {
  private readonly XmpDocument _doc = doc;

  public int Count =>
    _getBag()?.Elements(XmpNs.Rdf + "li").Count() ?? 0;

  public MpRegion this[int index] =>
    new(_getBag()?.Elements(XmpNs.Rdf + "li").ElementAt(index)
      ?? throw new ArgumentOutOfRangeException(nameof(index)));

  public IEnumerator<MpRegion> GetEnumerator() =>
    (_getBag()?.Elements(XmpNs.Rdf + "li") ?? [])
    .Select(e => new MpRegion(e))
    .GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() =>
    GetEnumerator();

  public MpRegion Add(string? name, string? rectangle = null) {
    var bag = _getOrCreateBag();
    var element = new XElement(XmpNs.Rdf + "li");
    bag.Add(element);

    var region = new MpRegion(element) {
      PersonDisplayName = name,
      Rectangle = rectangle
    };

    return region;
  }

  public void Remove(MpRegion region) {
    if (region.Element.Parent == null)
      return;

    region.Element.Remove();

    _removeIfEmpty();
  }

  public void Clear() {
    if (_getBag() is not { } bag) return;

    bag.RemoveNodes();
    _removeIfEmpty();
  }

  private XElement? _getBag() {
    var description = _doc.Rdf.GetXmpDescription(XmpNs.Mp);
    var regionInfo = description?.Element(XmpNs.Mp + "RegionInfo");
    var regions = regionInfo?.Descendants(XmpNs.MpRi + "Regions").FirstOrDefault();

    return regions?.Element(XmpNs.Rdf + "Bag");
  }

  private XElement _getOrCreateBag() {
    var desc = _doc.Rdf.GetOrCreateXmpDescription(XmpNs.Mp);
    var regionInfo = desc.GetOrCreateXmpElement(XmpNs.Mp + "RegionInfo");
    var regions = regionInfo.GetOrCreateXmpElement(XmpNs.MpRi + "Regions");
    var bag = regions.GetOrCreateXmpElement(XmpNs.Rdf + "Bag");

    return bag;
  }

  private void _removeIfEmpty() {
    if (_getBag() is not { } bag || bag.HasElements) return;

    var regions = bag.Parent;
    bag.Remove();

    if (regions?.HasElements != false) return;

    var regionInfo = regions.Parent;
    regions.Remove();

    if (regionInfo?.HasElements != false) return;

    regionInfo.Remove();

    _doc.RemoveEmptyDescriptions();
  }
}