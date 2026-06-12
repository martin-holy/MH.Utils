using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.DB;

public interface IRepository {
  bool IsModified { get; set; }
  bool AreRepoPropsModified { get; set; }
  int ChangesCount { get; }
  public int MaxId { get; set; }

  public int GetNextId();
}

public interface IRepository<T> : IRepository {
  public HashSet<T> All { get; set; }
}

public class Repository : IRepository {
  private bool _isModified;

  public bool IsModified { get => _isModified; set => _setIsModified(value); }
  public bool AreRepoPropsModified { get; set; }
  public int ChangesCount { get; private set; }
  public int MaxId { get; set; }

  public virtual int GetNextId() => ++MaxId;

  private void _setIsModified(bool value) {
    _isModified = value;

    if (value)
      ChangesCount++;
    else
      ChangesCount = 0;
  }
}

public class Repository<T> : Repository, IRepository<T> {
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

  public virtual IEnumerable<T> GetAll(Func<T, bool> where) =>
    All.Where(where);

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