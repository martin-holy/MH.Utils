using System;
using System.Xml.Linq;

namespace MH.Utils.Imaging.Xmp;

public static class XmpNs {
  public static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
  public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
  public static readonly XNamespace Exif = "http://ns.adobe.com/exif/1.0/";
  public static readonly XNamespace Tiff = "http://ns.adobe.com/tiff/1.0/";
  public static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
  public static readonly XNamespace Mp = "http://ns.microsoft.com/photo/1.2/";
  public static readonly XNamespace MpRi = "http://ns.microsoft.com/photo/1.2/t/RegionInfo#";
  public static readonly XNamespace MpReg = "http://ns.microsoft.com/photo/1.2/t/Region#";

  public static string GetPrefix(XNamespace ns) =>
    ns == Xmp ? "xmp" :
    ns == Dc ? "dc" :
    ns == Exif ? "exif" :
    ns == Tiff ? "tiff" :
    ns == Rdf ? "rdf" :
    ns == Mp ? "MP" :
    ns == MpRi ? "MPRI" :
    ns == MpReg ? "MPReg" :
    throw new ArgumentException($"Unknown XMP namespace '{ns.NamespaceName}'.", nameof(ns));
}