using System.Collections.Generic;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public static class XmpNs {
  public static readonly XNamespace X = "adobe:ns:meta/";
  public static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
  public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
  public static readonly XNamespace Exif = "http://ns.adobe.com/exif/1.0/";
  public static readonly XNamespace Tiff = "http://ns.adobe.com/tiff/1.0/";
  public static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
  public static readonly XNamespace MicrosoftPhoto = "http://ns.microsoft.com/photo/1.0/";
  public static readonly XNamespace Mp = "http://ns.microsoft.com/photo/1.2/";
  public static readonly XNamespace MpRi = "http://ns.microsoft.com/photo/1.2/t/RegionInfo#";
  public static readonly XNamespace MpReg = "http://ns.microsoft.com/photo/1.2/t/Region#";

  private static readonly Dictionary<XNamespace, string> _prefixes = new() {
    [X] = "x",
    [Xmp] = "xmp",
    [Dc] = "dc",
    [Exif] = "exif",
    [Tiff] = "tiff",
    [Rdf] = "rdf",
    [MicrosoftPhoto] = "MicrosoftPhoto",
    [Mp] = "MP",
    [MpRi] = "MPRI",
    [MpReg] = "MPReg"
  };

  public static string? GetPreferredPrefix(XNamespace ns) =>
    _prefixes.TryGetValue(ns, out var prefix) ? prefix : null;

  public static void SetPrefix(XNamespace ns, string prefix) =>
    _prefixes[ns] = prefix;
}