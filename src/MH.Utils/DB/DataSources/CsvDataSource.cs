using System;
using System.Collections.Generic;

namespace MH.Utils.DB.DataSources;

public interface ICsvDataSource : IDataSource {
  public SimpleDB DB { get; }
}

public abstract class CsvDataSource(SimpleDB db, string name, int propsCount) : DataSource(name), ICsvDataSource {
  protected string? _currentVolumeSerialNumber;

  public SimpleDB DB { get; } = db;
  public string FilePath { get; } = db.GetDBFilePath(name);
  public int PropsCount { get; } = propsCount;
  public bool IsDriveRelated { get; set; }
}

public abstract class CsvDataSource<T, TR, TLinkInfo>(SimpleDB db, string name, int propsCount, TR repository)
  : CsvDataSource(db, name, propsCount) where TR : IRepository<T> {

  public TR Repository { get; } = repository;
  public Dictionary<int, T> AllDict { get; } = [];

  protected virtual (T item, TLinkInfo linkInfo) _fromCsv(ReadOnlySpan<char> csv) => throw new NotImplementedException();
  protected virtual string _toCsv(T item) => throw new NotImplementedException();
  protected virtual void _addItem((T item, TLinkInfo linkInfo) data) => throw new NotImplementedException();
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
      ? _saveDriveRelated(_getAsDriveRelated())
      : _saveToSingleFile(Repository.All);

  protected bool _saveDriveRelated(Dictionary<string, IEnumerable<T>> drives) {
    var success = true;
    
    foreach (var (drive, items) in drives)
      success = success && SimpleDB.SaveToFile(items, _toCsv, DB.GetDBFilePath(drive, Name));

    return success;
  }

  protected bool _saveToSingleFile(IEnumerable<T> items) =>
    SimpleDB.SaveToFile(items, _toCsv, FilePath);

  protected virtual void _parseLine(string line) =>
    _addItem(_fromCsv(line.AsSpan()));
}