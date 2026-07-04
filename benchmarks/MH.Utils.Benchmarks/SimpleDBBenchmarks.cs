using BenchmarkDotNet.Attributes;
using MH.Utils.DB;
using MH.Utils.Extensions;
using MH.Utils.Imaging;

namespace MH.Utils.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class SimpleDBBenchmarks {
//| Method                        | Mean     | Error    | StdDev   | Gen0       | Allocated |
//|------------------------------ |---------:|---------:|---------:|-----------:|----------:|
//| ReadTableFileSplit            | 42.38 ms | 9.824 ms | 0.538 ms | 27538.4615 |  55.03 MB |
//| ReadTableFileNoParse          | 11.05 ms | 1.838 ms | 0.101 ms |  6703.1250 |  13.38 MB |
//| ReadTableFileSpanIdsAsString  | 33.56 ms | 3.946 ms | 0.216 ms | 13733.3333 |  27.44 MB |
//| ReadTableFileSpanIdsAsListInt | 38.18 ms | 4.532 ms | 0.248 ms | 20142.8571 |  40.23 MB |

  // 5.70 MB file with ~99k lines
  private const string _tableFilePath = "c:\\Programs\\PictureManager\\Debug\\net8.0-windows\\db\\Images.2046-671D.csv";
  private const int _propsCount = 11;
  private ImageM? _imageM;
  private ImageLinkInfo? _imageLinkInfo;

  //[Benchmark]
  public void ReadTableFileSplit() {
    SimpleDB.LoadFromFile(_parseLineSplit, _tableFilePath);
  }

  private void _parseLineSplit(string line) {
    var props = line.Split('|');
    if (props.Length != _propsCount)
      throw new ArgumentException("Incorrect number of values.", line);

    _imageM = _fromCsv(props);
  }

  private ImageM _fromCsv(string[] csv) =>
    new(int.Parse(csv[0]), csv[2]) {
      Width = csv[3].IntParseOrDefault(0),
      Height = csv[4].IntParseOrDefault(0),
      Orientation = (ImagingU.Orientation)csv[5].IntParseOrDefault(1),
      Rating = csv[6].IntParseOrDefault(0),
      Comment = string.IsNullOrEmpty(csv[7]) ? null : csv[7],
      IsOnlyInDb = csv[10] == "1"
    };

  //[Benchmark]
  public void ReadTableFileNoParse() {
    SimpleDB.LoadFromFile(_parseLineNoParse, _tableFilePath);
  }

  private void _parseLineNoParse(string line) { }

  //[Benchmark]
  public void ReadTableFileSpanIdsAsString() {
    DB.SimpleDB.LoadFromFile(_parseLineSpanIdsAsString, _tableFilePath);
  }

  // DB fields: ID|Folder|FileName|Width|Height|Orientation|Rating|Comment|People|Keywords|IsOnlyInDb
  private void _parseLineSpanIdsAsString(string line) {
    var spanLine = line.AsSpan();
    int start = 0;
    int field = 0;

    int id = 0;
    int folderId = 0;
    string fileName = string.Empty;
    int width = 0;
    int height = 0;
    int orientation = 1;
    int rating = 0;
    string? comment = null;
    string personIds = string.Empty;
    string keywordIds = string.Empty;
    bool isOnlyInDb = false;

    for (int i = 0; i <= spanLine.Length; i++) {
      if (i == spanLine.Length || spanLine[i] == '|') {
        var slice = spanLine.Slice(start, i - start);

        switch (field) {
          case 0: id = int.Parse(slice); break;
          case 1: folderId = int.Parse(slice); break;
          case 2: fileName = slice.ToString(); break;
          case 3: width = _intParseOrDefault(slice, 0); break;
          case 4: height = _intParseOrDefault(slice, 0); break;
          case 5: orientation = _intParseOrDefault(slice, 1); break;
          case 6: rating = _intParseOrDefault(slice, 0); break;
          case 7: comment = slice.Length == 0 ? null : slice.ToString(); break;
          case 8: personIds = slice.ToString(); break;
          case 9: keywordIds = slice.ToString(); break;
          case 10: isOnlyInDb = slice.Length == 1 && slice[0] == '1'; break;
        }

        field++;
        start = i + 1;
      }
    }

    _imageM = new ImageM(id, fileName) {
      Width = width,
      Height = height,
      Orientation = (ImagingU.Orientation)orientation,
      Rating = rating,
      Comment = comment,
      IsOnlyInDb = isOnlyInDb
    };

    _imageLinkInfo = new ImageLinkInfo(folderId, personIds, keywordIds);
  }

  private static int _intParseOrDefault(ReadOnlySpan<char> s, int d) =>
    int.TryParse(s, out var result) ? result : d;

  private class ImageM {
    public int Id { get; }
    public string FileName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ImagingU.Orientation Orientation { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsOnlyInDb { get; set; }

    public ImageM(int id, string fileName) {
      Id = id;
      FileName = fileName;
    }
  }

  private readonly struct ImageLinkInfo(int folderId, string personIds, string keywordIds) {
    public readonly int FolderId = folderId;
    public readonly string PersonIds = personIds;
    public readonly string KeywordIds = keywordIds;
  }

//| Method                 | Mean      | Error      | StdDev    | Gen0   | Allocated |
//|----------------------- |----------:|-----------:|----------:|-------:|----------:|
//| IdToRecord1            | 525.12 ns | 246.828 ns | 13.529 ns | 0.3405 |     712 B |
//| IdToRecord2            |  75.35 ns |  65.846 ns |  3.609 ns | 0.0612 |     128 B |
//| IdToRecord3            |  85.08 ns |   1.695 ns |  0.093 ns | 0.0918 |     192 B |
//| IdToRecord4            | 121.98 ns |  17.137 ns |  0.939 ns | 0.0842 |     176 B |
//| IdToRecord5            |  73.77 ns |  29.045 ns |  1.592 ns | 0.0612 |     128 B |
//| IdToRecord6_state      |  78.39 ns |  42.630 ns |  2.337 ns | 0.0612 |     128 B |
//| IdToRecord7_tuplestate |  76.80 ns |  12.312 ns |  0.675 ns | 0.0612 |     128 B |
  string _ids = "12,54,897,9794,9744987,487";

  //[Benchmark]
  public void IdToRecord1() {
    var ids = IdToRecord1(_ids);
  }

  //[Benchmark]
  public void IdToRecord2() {
    var ids = IdToRecord2(_ids);
  }

  //[Benchmark]
  public void IdToRecord3() {
    var ids = IdToRecord3(_ids);
  }

  //[Benchmark]
  public void IdToRecord4() {
    var ids = IdToRecord4(_ids);
  }

  //[Benchmark]
  public void IdToRecord5() {
    var ids = IdToRecord5(_ids);
  }

  //[Benchmark]
  public void IdToRecord6_state() {
    var ids = IdToRecord6(_ids);
  }

  //[Benchmark]
  public void IdToRecord7_tuplestate() {
    var ids = IdToRecord7(_ids);
  }

  public static List<int>? IdToRecord1(string csv) {
    if (string.IsNullOrEmpty(csv)) return null;

    var items = csv
      .Split(',')
      .Select(int.Parse)
      .Select(x => (int?)x)
      .Where(x => x != null)
      .Select(x => (int)x!)
      .ToList();

    return items.Count == 0 ? null : items;
  }

  public static List<int>? IdToRecord2(string csv) {
    if (string.IsNullOrEmpty(csv)) return null;

    List<int> result = [];
    int value = 0;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == ',') {
        result.Add(value);
        value = 0;
        continue;
      }

      value = value * 10 + (csv[i] - '0');
    }

    return result.Count == 0 ? null : result;
  }

  public static List<int>? IdToRecord3(string csv) {
    if (string.IsNullOrEmpty(csv)) return null;

    List<int> result = [];

    IdParser3(csv, result.Add);
    
    return result.Count == 0 ? null : result;
  }

  public static void IdParser3(string csv, Action<int> action) {
    int value = 0;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == ',') {
        action(value);
        value = 0;
        continue;
      }

      value = value * 10 + (csv[i] - '0');
    }
  }

  public static List<int>? IdToRecord4(string csv) {
    if (string.IsNullOrEmpty(csv)) return null;

    List<int> result = [];

    foreach (var id in IdParser4(csv)) {
      result.Add(id);
    }

    return result.Count == 0 ? null : result;
  }

  public static IEnumerable<int> IdParser4(string csv) {
    int value = 0;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == ',') {
        yield return value;
        value = 0;
        continue;
      }

      value = value * 10 + (csv[i] - '0');
    }
  }

  public static void IdParser5(string csv, List<int> result) {
    int value = 0;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == ',') {
        result.Add(value);
        value = 0;
        continue;
      }

      value = value * 10 + (csv[i] - '0');
    }
  }

  public static List<int>? IdToRecord5(string csv) {
    if (string.IsNullOrEmpty(csv))
      return null;

    List<int> result = [];

    IdParser5(csv, result);

    return result;
  }

  public static List<int>? IdToRecord6(string csv) {
    if (string.IsNullOrEmpty(csv))
      return null;

    List<int> result = [];

    CsvParser.ParseInts(csv, result, static (list, id) => list.Add(id));

    return result;
  }

  public List<int>? IdToRecord7(string csv) {
    if (string.IsNullOrEmpty(csv))
      return null;

    List<int> result = [];

    CsvParser.ParseInts(csv, (result, this), static (state, id) => state.result.Add(id));

    return result;
  }

  private Dictionary<int, string> _dict = new() { { 1, "1" }, { 50, "50" }, { 487, "487" } };

  //[Benchmark]
  public void IdsToRecords1() {
    var x = IdsToRecords("1,50,4,487", _dict);
  }

  //[Benchmark]
  public void IdsToRecords1AllFounded() {
    var x = IdsToRecords("1,50,487", _dict);
  }

  //[Benchmark]
  public void IdsToRecords1Empty() {
    var x = IdsToRecords(string.Empty, _dict);
  }

  //[Benchmark]
  public void IdsToRecords2() {
    var x = IdsToRecords2("1,50,4,487", _dict);
  }

  //[Benchmark]
  public void IdsToRecords2AllFounded() {
    var x = IdsToRecords2("1,50,487", _dict);
  }

  //[Benchmark]
  public void IdsToRecords2Empty() {
    var x = IdsToRecords2(string.Empty, _dict);
  }

  public static Tuple<List<TI>, List<int>>? IdsToRecords<TI>(string csv, Dictionary<int, TI> source) {
    if (string.IsNullOrEmpty(csv)) return null;
    var found = new List<TI>();
    var notFound = new List<int>();

    foreach (var id in csv.Split(',').Select(int.Parse))
      if (source.TryGetValue(id, out var rec))
        found.Add(rec);
      else
        notFound.Add(id);

    return new(found, notFound);
  }

  public static Tuple<List<TI>?, List<int>?>? IdsToRecords2<TI>(string csv, Dictionary<int, TI> source) {
    if (string.IsNullOrEmpty(csv)) return null;

    List<TI>? found = null;
    List<int>? notFound = null;
    int id = 0;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == ',') {
        if (source.TryGetValue(id, out var rec)) {
          found ??= [];
          found.Add(rec);
        }
        else {
          notFound ??= [];
          notFound.Add(id);
        }

        id = 0;
        continue;
      }

      id = id * 10 + (csv[i] - '0');
    }

    return new(found, notFound);
  }

//| Method   | Mean      | Error     | StdDev    | Allocated |
//|--------- |----------:|----------:|----------:|----------:|
//| IdParse1 | 10.977 ns | 0.7219 ns | 0.0396 ns |         - |
//| IdParse2 |  3.843 ns | 2.1276 ns | 0.1166 ns |         - |
  //[Benchmark]
  public void IdParse1() {
    int id = int.Parse("487");
  }

  //[Benchmark]
  public void IdParse2() {
    int id = CsvParser.ParseInt("487");
  }  
}