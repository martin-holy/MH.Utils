using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class DataAdapter(SimpleDB db, string name, int propsCount) : IDataAdapter {
  private bool _isModified;
  protected string? _currentVolumeSerialNumber;

  public SimpleDB DB { get; } = db;
  public string Name { get; } = name;
  public string FilePath { get; } = db.GetDBFilePath(name);
  public int PropsCount { get; } = propsCount;
  public int MaxId { get; set; }
  public bool IsDriveRelated { get; set; }

  public bool IsModified {
    get => _isModified;
    set {
      _isModified = value;
      if (value) DB.AddChange();
    }
  }

  public virtual void Load() => throw new NotImplementedException();
  public virtual void Save() => throw new NotImplementedException();
  public virtual int GetNextId() => ++MaxId;
}

public class DataAdapter<T>(SimpleDB db, string name, int propsCount) : DataAdapter(db, name, propsCount) {
  public HashSet<T> All { get; set; } = [];

  public event EventHandler<T>? ItemCreatedEvent;
  public event EventHandler<T>? ItemUpdatedEvent;
  public event EventHandler<T>? ItemDeletedEvent;
  public event EventHandler<IList<T>>? ItemsDeletedEvent;

  protected void _raiseItemCreated(T item) => ItemCreatedEvent?.Invoke(this, item);
  protected void _raiseItemUpdated(T item) => ItemUpdatedEvent?.Invoke(this, item);
  protected void _raiseItemDeleted(T item) => ItemDeletedEvent?.Invoke(this, item);
  protected void _raiseItemsDeleted(IList<T> items) => ItemsDeletedEvent?.Invoke(this, items);

  protected virtual void _onItemCreated(object sender, T item) { }
  protected virtual void _onItemUpdated(object sender, T item) { }
  protected virtual void _onItemDeleted(object sender, T item) { }
  protected virtual void _onItemsDeleted(object sender, IList<T> items) { }

  protected virtual T _fromCsv(string[] csv) => throw new NotImplementedException();
  protected virtual string _toCsv(T item) => throw new NotImplementedException();
  protected virtual void _addItem(T item, string[] props) => throw new NotImplementedException();
  protected virtual Dictionary<string, IEnumerable<T>> _getAsDriveRelated() => throw new NotImplementedException();

  public virtual IEnumerable<T> GetAll(Func<T, bool> where) =>
    All.Where(where);

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

  public override void Save() {
    if (IsDriveRelated)
      _saveDriveRelated(_getAsDriveRelated());
    else
      _saveToSingleFile(All);
  }

  protected void _saveDriveRelated(Dictionary<string, IEnumerable<T>> drives) {
    foreach (var (drive, items) in drives)
      SimpleDB.SaveToFile(items, _toCsv, DB.GetDBFilePath(drive, Name));

    // TODO should be for each drive
    IsModified = false;

    // TODO remove in future release
    if (File.Exists(FilePath))
      File.Delete(FilePath);
  }

  protected void _saveToSingleFile(IEnumerable<T> items) {
    if (SimpleDB.SaveToFile(items, _toCsv, FilePath))
      IsModified = false;
  }

  protected virtual void _parseLine(string line) {
    var props = line.Split('|');
    if (props.Length != PropsCount)
      throw new ArgumentException("Incorrect number of values.", line);

    _addItem(_fromCsv(props), props);
  }

  public virtual void Modify(T item) {
    IsModified = true;
  }

  public virtual T ItemCreate(T item) {
    All.Add(item);
    IsModified = true;
    _raiseItemCreated(item);
    _onItemCreated(this, item);
    return item;
  }

  public virtual void ItemDelete(T item, bool singleDelete = true) {
    if (singleDelete) {
      ItemsDelete([item]);
      return;
    }

    All.Remove(item);
    IsModified = true;
    _raiseItemDeleted(item);
  }

  public virtual void ItemsDelete(IList<T>? items) {
    if (items == null || items.Count == 0) return;
    foreach (var item in items) ItemDelete(item, false);
    _raiseItemsDeleted(items);
    _onItemsDeleted(this, items);
    foreach (var item in items) _onItemDeleted(this, item);
  }
}