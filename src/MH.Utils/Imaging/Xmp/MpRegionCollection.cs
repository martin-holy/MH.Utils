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
    new(_getDescription(_getBag()?.Elements(XmpNs.Rdf + "li").ElementAt(index)
      ?? throw new ArgumentOutOfRangeException(nameof(index))));

  public IEnumerator<MpRegion> GetEnumerator() =>
    (_getBag()?.Elements(XmpNs.Rdf + "li") ?? [])
    .Select(e => new MpRegion(_getDescription(e)))
    .GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator() =>
    GetEnumerator();

  public MpRegion Add(string? name, string? rectangle = null) {
    var bag = _getOrCreateBag();
    var description = new XElement(XmpNs.Rdf + "Description");
    var item = new XElement(XmpNs.Rdf + "li", description);
    bag.Add(item);

    var region = new MpRegion(description) {
      PersonDisplayName = name,
      Rectangle = rectangle
    };

    return region;
  }

  public void Remove(MpRegion region) {
    var item = region.Element.Name == XmpNs.Rdf + "Description"
      ? region.Element.Parent
      : region.Element;

    if (item?.Parent == null) return;

    item.Remove();
    _removeIfEmpty();
  }

  public void Clear() {
    if (_getBag() is not { } bag) return;

    bag.RemoveNodes();
    _removeIfEmpty();
  }

  private static XElement _getDescription(XElement item) {
    if (item.Element(XmpNs.Rdf + "Description") is { } description)
      return description;

    description = new XElement(XmpNs.Rdf + "Description");

    foreach (var node in item.Nodes().ToList()) {
      node.Remove();
      description.Add(node);
    }

    foreach (var attribute in item.Attributes().ToList()) {
      attribute.Remove();

      if (attribute.Name.Namespace == XmpNs.MpReg) {
        if (description.Element(attribute.Name) == null)
          description.Add(new XElement(attribute.Name, attribute.Value));
      }
      else {
        description.Add(attribute);
      }
    }

    item.Add(description);

    return description;
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
    var regionsDescription = regionInfo.Element(XmpNs.Rdf + "Description");

    if (regionsDescription == null) {
      regionsDescription = new XElement(XmpNs.Rdf + "Description");

      if (regionInfo.Element(XmpNs.MpRi + "Regions") is { } existingRegions) {
        existingRegions.Remove();
        regionsDescription.Add(existingRegions);
      }

      regionInfo.Add(regionsDescription);
    }

    var regions = regionsDescription.GetOrCreateXmpElement(XmpNs.MpRi + "Regions");
    var bag = regions.GetOrCreateXmpElement(XmpNs.Rdf + "Bag");

    foreach (var item in bag.Elements(XmpNs.Rdf + "li").ToList())
      _getDescription(item);

    return bag;
  }

  private void _removeIfEmpty() {
    if (_getBag() is not { } bag || bag.HasElements) return;
    if (bag.Parent is not { } regions) return;

    bag.Remove();

    if (regions.HasElements) return;
    if (regions.Parent is not { } regionsParent) return;

    if (regionsParent.Name == XmpNs.Rdf + "Description") {
      var regionInfo = regionsParent.Parent;
      regionsParent.Remove();

      if (regionInfo?.HasElements != false) return;

      regionInfo.Remove();
    }
    else {
      regionsParent.Remove();
    }

    _doc.RemoveEmptyDescriptions();
  }
}