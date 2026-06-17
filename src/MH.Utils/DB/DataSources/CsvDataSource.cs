using System;
using System.Collections.Generic;
using System.IO;

namespace MH.Utils.DB.DataSources;

public interface ICsvDataSource : IDataSource {
  public SimpleDB DB { get; }
}

public abstract class CsvDataSource(SimpleDB db, string name, int fieldsCount) : DataSource(name), ICsvDataSource {
  protected string? _currentVolumeSerialNumber;
  // TODO try to extract props to be optional
  protected string _propsFilePath = Path.Combine(db.DbDir, $"{name}_props.csv");
  protected Dictionary<string, string>? _props = null;

  public SimpleDB DB { get; } = db;
  public string FilePath { get; } = db.GetDBFilePath(name);
  public int FieldsCount { get; } = fieldsCount;
  public bool IsDriveRelated { get; set; }

  protected void _validateFieldsCount(int fieldsCount, ReadOnlySpan<char> csv) {
    if (fieldsCount != FieldsCount)
      throw new ArgumentException("Incorrect number of values.", csv.ToString());
  }
}

public abstract class CsvDataSource<T>(SimpleDB db, string name, int fieldsCount)
  : CsvDataSource(db, name, fieldsCount) {

  protected virtual void _parseLine(string line) => throw new NotImplementedException();
  protected virtual T _fromCsv(ReadOnlySpan<char> csv) => throw new NotImplementedException();
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