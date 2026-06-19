using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.DB.DataSources;

public interface ICsvDataSource : IDataSource {
  SimpleDB DB { get; }

  void LinkProps();
  bool HaveProps();
  void Clear();
}

public interface ICsvRelationDataSource : ICsvDataSource, IRelationDataSource;

public readonly struct NoLinkInfo;

public abstract class CsvDataSource(SimpleDB db, string name, int fieldsCount) : DataSource(name), ICsvDataSource {
  protected string? _currentVolumeSerialNumber;

  protected string _propsFilePath = Path.Combine(db.DbDir, $"{name}_props.csv");
  protected Dictionary<string, string>? _props = null;

  public SimpleDB DB { get; } = db;
  public string FilePath => DB.GetDBFilePath(Name);
  public int FieldsCount { get; } = fieldsCount;
  public bool IsDriveRelated { get; set; }

  protected virtual Dictionary<string, string>? _propsToCsv() => null;

  public virtual void LinkProps() { }

  public bool HaveProps() => _props != null;

  public override void LoadProps() =>
    SimpleDB.LoadFromFile(
      line => {
        var prop = line.Split('|');
        if (prop.Length != 2)
          throw new ArgumentException("Incorrect number of values.", line);
        (_props ??= []).Add(prop[0], prop[1]);
      }, _propsFilePath);

  public override bool SaveProps() {
    var props = _propsToCsv();
    if (props?.Count > 0 != true) return true;
    return SimpleDB.SaveToFile(props.Select(x => $"{x.Key}|{x.Value}"), x => x, _propsFilePath);
  }

  public virtual void Clear() {
    _props = null;
  }

  protected void _validateFieldsCount(int fieldsCount, ReadOnlySpan<char> csv) {
    if (fieldsCount != FieldsCount)
      throw new ArgumentException("Incorrect number of values.", csv.ToString());
  }
}

public abstract class CsvDataSource<T, TLinkInfo>(SimpleDB db, string name, int fieldsCount)
  : CsvDataSource(db, name, fieldsCount) {

  protected virtual void _parseLine(string line) => throw new NotImplementedException();
  protected virtual (T item, TLinkInfo linkInfo) _fromCsv(ReadOnlySpan<char> csv) => throw new NotImplementedException();
  protected virtual string _toCsv(T item) => throw new NotImplementedException();
  protected virtual IEnumerable<T> _getAll() => throw new NotImplementedException();
  protected virtual Dictionary<string, IEnumerable<T>> _getAsDriveRelated() => throw new NotImplementedException();

  public override void Load() {
    if (IsDriveRelated)
      _loadDriveRelated();
    else
      _loadFromSingleFile();
  }

  protected void _loadDriveRelated() {
    foreach (var drive in Drives.SerialNumbers) {
      _currentVolumeSerialNumber = drive.Value;
      SimpleDB.LoadFromFile(_parseLine, DB.GetDBFilePath(drive.Key, Name));
    }
  }

  protected void _loadFromSingleFile() =>
    SimpleDB.LoadFromFile(_parseLine, FilePath);

  public override bool Save() =>
    IsDriveRelated
      ? _saveAsDriveRelated(_getAsDriveRelated())
      : _saveToSingleFile(_getAll());

  protected bool _saveAsDriveRelated(Dictionary<string, IEnumerable<T>> drives) {
    var success = true;

    foreach (var (drive, items) in drives)
      success = success && SimpleDB.SaveToFile(items, _toCsv, DB.GetDBFilePath(drive, Name));

    return success;
  }

  protected bool _saveToSingleFile(IEnumerable<T> items) =>
    SimpleDB.SaveToFile(items, _toCsv, FilePath);
}