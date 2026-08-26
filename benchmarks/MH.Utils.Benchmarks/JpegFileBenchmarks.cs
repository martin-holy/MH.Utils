using BenchmarkDotNet.Attributes;
using MH.Utils.Imaging;
using MH.Utils.Imaging.Exif;
using MH.Utils.Imaging.Jpeg;
using MH.Utils.Imaging.Xmp;
using System.Xml.Linq;

namespace MH.Utils.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class JpegFileBenchmarks {
  private string _path = null!;

  [GlobalSetup]
  public void Setup() {
    _path = @"c:\Programs\-=Graphics\ExifTool\a_comment.jpg";
  }

  /*| Method               | Mean      | Error     | StdDev    | Gen0    | Allocated |
    |--------------------- |----------:|----------:|----------:|--------:|----------:|
    | GetXmpResource       | 96.74 us  | 14.33 us  | 0.785 us  | 21.2402 | 43.46 KB  |
    | GetXmpDoc            | 100.09 us | 187.28 us | 10.265 us | 20.9961 | 43.29 KB  |
    | GetDescAttrOrElement | 92.38 us  | 12.03 us  | 0.659 us  | 20.9961 | 43.38 KB  |*/
  //[Benchmark]
  public void GetXmpResource() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    _ = jpeg.Xmp.Doc!.Document.Root!.GetXmpPropertyParent(XmpNs.Xmp + "CreatorTool");
  }

  //[Benchmark]
  public void GetXmpDoc() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    _ = jpeg.Xmp.Doc!.Document;
  }

  //[Benchmark]
  public void GetDescAttrOrElement() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    var name = XmpNs.Xmp + "CreatorTool";
    _ = jpeg.Xmp.Doc!.Document.Descendants(XmpNs.Rdf + "Description")
        .FirstOrDefault(d => d.Attribute(name) != null || d.Element(name) != null);
  }

  /*| Method               | Mean     | Error     | StdDev   | Gen0    | Allocated |
    |--------------------- |---------:|----------:|---------:|--------:|----------:|
    | KeywordsLinq         | 97.48 us | 26.394 us | 1.447 us | 21.2402 |  43.46 KB |
    | KeywordsListContains | 96.72 us |  7.249 us | 0.397 us | 20.9961 |  43.38 KB |
    | KeywordsListHashSet  | 94.33 us | 15.681 us | 0.860 us | 20.9961 |  43.38 KB |*/
  [Benchmark]
  public void KeywordsLinq() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    _ = jpeg.Xmp.GetKeywords();
  }

  //[Benchmark]
  public void KeywordsListContains() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    //_ = jpeg.Xmp.GetKeywordsListContains();
  }

  //[Benchmark]
  public void KeywordsListHashSet() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    //_ = jpeg.Xmp.GetKeywordsListHashSet();
  }

  /*| Method         | Mean      | Error     | StdDev   | Gen0    | Allocated |
    |--------------- |----------:|----------:|---------:|--------:|----------:|
    | Load           |  64.56 us |  5.301 us | 0.291 us | 10.6201 |  21.88 KB |
    | Keywords       |  96.90 us | 18.568 us | 1.018 us | 21.2402 |  43.43 KB |
    | People         |  64.90 us |  4.784 us | 0.262 us | 10.7422 |  21.95 KB |
    | PeopleKeywords | 100.85 us | 40.864 us | 2.240 us | 21.9727 |  45.26 KB |
    | GeoNameId      |  95.80 us |  2.590 us | 0.142 us | 21.2402 |  43.58 KB |*/
  //[Benchmark]
  public void Load() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    _ = jpeg.Xmp;
  }

  //[Benchmark]
  public void Keywords() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Xmp);
    _ = metadata.Keywords;
  }

  //[Benchmark]
  public void People() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Xmp);
    _ = metadata.People;
  }

  //[Benchmark]
  public void PeopleKeywords() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Xmp);
    _ = _readPeopleSegmentsKeywords(metadata.People);
  }

  //[Benchmark]
  public void GeoNameId() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Xmp);
    _ = _getGeoNameId(metadata);
  }

  /*| Method       | Mean      | Error      | StdDev    | Gen0    | Allocated |
    |------------- |----------:|-----------:|----------:|--------:|----------:|
    | ExifRead     |  61.73 us |   7.586 us |  0.416 us | 17.8223 |  36.59 KB |
    | XmpRead      |  63.80 us |  13.768 us |  0.755 us | 10.6201 |  21.88 KB |
    | ReadExifOnly |  60.75 us |   9.668 us |  0.530 us | 17.8223 |  37.07 KB |
    | ReadXmpOnly  | 103.96 us |   8.765 us |  0.480 us | 22.2168 |  45.75 KB |
    | ReadAll      | 180.57 us | 673.818 us | 36.934 us | 38.5742 |  79.85 KB |*/

  //[Benchmark]
  public void ExifRead() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Exif);
    _ = jpeg.Exif;
  }

  //[Benchmark]
  public void XmpRead() {
    var jpeg = new JpegFile(_path, JpegMetadataLoad.Xmp);
    _ = jpeg.Xmp;
  }

  //[Benchmark]
  public void ReadExifOnly() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Exif);

    _ = metadata.Orientation;
    _ = metadata.GpsCoordinate;
  }

  //[Benchmark]
  public void ReadXmpOnly() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.Xmp);

    _ = metadata.Keywords;
    _ = _readPeopleSegmentsKeywords(metadata.People);
    _ = _getGeoNameId(metadata);
  }

  //[Benchmark]
  public void ReadAll() {
    var metadata = new ImageMetadata(_path, JpegMetadataLoad.All);

    _ = metadata.Width;
    _ = metadata.Height;
    _ = metadata.Rating;
    _ = metadata.Comment;
    _ = metadata.Orientation;
    _ = metadata.Keywords;
    _ = _readPeopleSegmentsKeywords(metadata.People);
    _ = _getGeoNameId(metadata);
    _ = metadata.GpsCoordinate;
  }

  /*| Method  | Mean     | Error    | StdDev   | Gen0    | Allocated |
    |-------- |---------:|---------:|---------:|--------:|----------:|
    | ReadAll | 189.9 us | 812.0 us | 44.51 us | 38.5742 |  79.96 KB |*/
  //[Benchmark]
  public void ReadAllToMim() {
    var mim = new MediaItemMetadata(_path);
    _readAll(mim);
  }

  private static void _readAll(MediaItemMetadata mim) {
    var metadata = new ImageMetadata(mim.FilePath, JpegMetadataLoad.All);

    var width = metadata.Width;
    var height = metadata.Height;
    if (!width.HasValue || !height.HasValue) return;

    mim.Width = width.Value;
    mim.Height = height.Value;
    mim.Rating = metadata.Rating ?? 0;
    mim.Comment = StringUtils.NormalizeComment(metadata.Comment);
    mim.Orientation = metadata.Orientation.ToMsOrientation() ?? Orientation.Normal;
    mim.Keywords = metadata.Keywords;
    mim.PeopleSegmentsKeywords = _readPeopleSegmentsKeywords(metadata.People);
    mim.GeoNameId = _getGeoNameId(metadata);

    if (metadata.GpsCoordinate is { } gps) {
      mim.Lat = gps.Latitude;
      mim.Lng = gps.Longitude;
    }

    mim.Success = true;
  }

  private static List<Tuple<string, List<Tuple<string, string[]?>>>>? _readPeopleSegmentsKeywords(MpRegionCollection? people) {
    if (people == null || people.Count == 0) return null;

    var output = new List<Tuple<string, List<Tuple<string, string[]?>>>>();

    foreach (var region in people) {
      var name = region.PersonDisplayName;
      var rect = region.Rectangle;

      if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(rect))
        continue;

      var keywords = region.Element
        .GetXmpArray(XmpNs.MpReg + "RectangleKeywords")?
        .Select(e => e.Value.Trim())
        .Where(v => v.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

      var person = output.FirstOrDefault(x =>
        string.Equals(x.Item1, name, StringComparison.OrdinalIgnoreCase));

      if (person == null) {
        person = new(name, []);
        output.Add(person);
      }

      person.Item2.Add(new(rect, keywords?.Length > 0 ? keywords : null));
    }

    return output.Count > 0 ? output : null;
  }

  private static readonly XNamespace _nsMhu = "https://github.com/martin-holy/MH.Utils/xmp";
  private static readonly XNamespace _nsGeoNames = "GeoNames";

  private static int? _getGeoNameId(ImageMetadata metadata) =>
    metadata.Jpeg.Xmp.Doc is not { } doc
      ? null
      : doc.GetInt(_nsMhu + "GeoNameId") ??
        doc.GetInt(_nsGeoNames + "GeoNameId"); // this is old namespace I used before

  private class MediaItemMetadata(string filePath) {
    public string FilePath { get; set; } = filePath;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Orientation Orientation { get; set; }
    public bool Success { get; set; }
    public string[]? Keywords { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public int? GeoNameId { get; set; }
    public List<Tuple<string, List<Tuple<string, string[]?>>>>? PeopleSegmentsKeywords { get; set; }
  }

  private static class StringUtils {
    private static readonly HashSet<char> _commentAllowedCharacters = new("@#$€_&+-()*'.:;!?=<>% ");

    public static string? NormalizeComment(string? comment) =>
      string.IsNullOrEmpty(comment)
        ? null
        : new string(comment.Where(x => char.IsLetterOrDigit(x) || _commentAllowedCharacters.Contains(x)).ToArray());
  }
}