using System;
using System.Collections.Generic;

namespace MH.Utils.DB.Repositories;

public interface IRepository : IDbTrackable {
  int MaxId { get; set; }
  int GetNextId();
}

public interface IRepository<T> : IRepository {
  HashSet<T> All { get; }
  IEnumerable<T> GetAll(Func<T, bool> where);
  void SetAll(HashSet<T> items);
  void Modify(T item);
}

public class Repository : DbTrackable, IRepository {
  public int MaxId { get; set; }

  public virtual int GetNextId() => ++MaxId;
}

public class Repository<T> : Repository, IRepository<T> {
  public HashSet<T> All { get; private set; } = [];

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

  public virtual IEnumerable<T> GetAll(Func<T, bool> where) {
    foreach (var item in All)
      if (where(item))
        yield return item;
  }

  public void SetAll(HashSet<T> items) =>
    All = items;

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