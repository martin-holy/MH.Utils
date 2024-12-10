using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class DataAdapter : IDataAdapter {
  private bool _isModified;
  protected string? _currentVolumeSerialNumber;

  public SimpleDB DB { get; }
  public string Name { get; }
  public string FilePath { get; }
  public int PropsCount { get; }
  public int MaxId { get; set; }
  public bool IsDriveRelated { get; set; }

  public bool IsModified {
    get => _isModified;
    set {
      _isModified = value;
      if (value)
        DB.AddChange();
    }
  }

  public DataAdapter(SimpleDB db, string name, int propsCount) {
    DB = db;
    Name = name;
    PropsCount = propsCount;
    FilePath = db.GetDBFilePath(name);
  }

  public virtual void Load() => throw new NotImplementedException();
  public virtual void Save() => throw new NotImplementedException();
  public virtual int GetNextId() => ++MaxId;
}

public class DataAdapter<T> : DataAdapter {
  public HashSet<T> All { get; set; } = [];

  public event EventHandler<T>? ItemCreatedEvent;
  public event EventHandler<T>? ItemUpdatedEvent;
  public event EventHandler<T>? ItemDeletedEvent;
  public event EventHandler<IList<T>>? ItemsDeletedEvent;

  public DataAdapter(SimpleDB db, string name, int propsCount) : base(db, name, propsCount) { }

  protected void _raiseItemCreated(T item) => ItemCreatedEvent?.Invoke(this, item);
  protected void _raiseItemUpdated(T item) => ItemUpdatedEvent?.Invoke(this, item);
  protected void _raiseItemDeleted(T item) => ItemDeletedEvent?.Invoke(this, item);
  protected void _raiseItemsDeleted(IList<T> items) => ItemsDeletedEvent?.Invoke(this, items);

  protected virtual void OnItemCreated(object sender, T item) { }
  protected virtual void OnItemUpdated(object sender, T item) { }
  protected virtual void OnItemDeleted(object sender, T item) { }
  protected virtual void OnItemsDeleted(object sender, IList<T> items) { }

  public virtual T FromCsv(string[] csv) => throw new NotImplementedException();
  public virtual string ToCsv(T item) => throw new NotImplementedException();
  public virtual void AddItem(T item, string[] props) => throw new NotImplementedException();
  public virtual Dictionary<string, IEnumerable<T>> GetAsDriveRelated() => throw new NotImplementedException();

  public virtual IEnumerable<T> GetAll(Func<T, bool> where) =>
    All.Where(where);

  public override void Load() {
    if (IsDriveRelated)
      LoadDriveRelated();
    else
      LoadFromSingleFile();
  }

  public void LoadDriveRelated() {
    foreach (var drive in Drives.SerialNumbers) {
      _currentVolumeSerialNumber = drive.Value;
      SimpleDB.LoadFromFile(ParseLine, DB.GetDBFilePath(drive.Key, Name));
    }
  }

  public void LoadFromSingleFile() =>
    SimpleDB.LoadFromFile(ParseLine, FilePath);

  public override void Save() {
    if (IsDriveRelated)
      SaveDriveRelated(GetAsDriveRelated());
    else
      SaveToSingleFile(All);
  }

  public void SaveDriveRelated(Dictionary<string, IEnumerable<T>> drives) {
    foreach (var (drive, items) in drives)
      SimpleDB.SaveToFile(items, ToCsv, DB.GetDBFilePath(drive, Name));

    // TODO should be for each drive
    IsModified = false;

    // TODO remove in future release
    if (File.Exists(FilePath))
      File.Delete(FilePath);
  }

  public void SaveToSingleFile(IEnumerable<T> items) {
    if (SimpleDB.SaveToFile(items, ToCsv, FilePath))
      IsModified = false;
  }

  public virtual void ParseLine(string line) {
    var props = line.Split('|');
    if (props.Length != PropsCount)
      throw new ArgumentException("Incorrect number of values.", line);

    AddItem(FromCsv(props), props);
  }

  public virtual void Modify(T item) {
    IsModified = true;
  }

  public virtual T ItemCreate(T item) {
    All.Add(item);
    IsModified = true;
    _raiseItemCreated(item);
    OnItemCreated(this, item);
    return item;
  }

  public virtual void ItemDelete(T item, bool singleDelete = true) {
    if (singleDelete) {
      ItemsDelete(new[] { item });
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
    OnItemsDeleted(this, items);
    foreach (var item in items) OnItemDeleted(this, item);
  }
}