using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.DB.Repositories;

public interface ITreeRepository<T> : IRepository<T> where T : class, ITreeItem {
  public event EventHandler<T> ItemCreatedEvent;
  public T ItemCreate(ITreeItem parent, string name);
  public void ItemRename(ITreeItem item, string name);
  public void ItemCopy(ITreeItem item, ITreeItem dest);
  public void ItemMove(ITreeItem item, ITreeItem dest, bool aboveDest);
  public void ItemDelete(ITreeItem item);
  public void TreeItemDelete(ITreeItem item);
  public string? ValidateNewItemName(ITreeItem parent, string? name);
}

public class TreeRepository<T> : Repository<T>, ITreeRepository<T> where T : class, ITreeItem {
  public event EventHandler<T>? ItemRenamedEvent;
  public event EventHandler<T>? ItemMovedEvent;

  public virtual T ItemCreate(ITreeItem parent, string name) => throw new NotImplementedException();
  public virtual void ItemCopy(ITreeItem item, ITreeItem dest) => throw new NotImplementedException();

  protected void _raiseItemRenamed(T item) => ItemRenamedEvent?.Invoke(this, item);
  protected void _raiseItemMoved(T item) => ItemMovedEvent?.Invoke(this, item);

  protected virtual void _onItemRenamed(T item) { }

  public virtual T TreeItemCreate(T item) {
    if (item.Parent != null)
      TreeU.SetInOrder(item.Parent.Items, item, x => x.Name);

    return ItemCreate(item);
  }

  public virtual void ItemRename(ITreeItem item, string name) {
    item.Name = name;

    if (item.Parent != null)
      TreeU.SetInOrder(item.Parent.Items, item, x => x.Name);

    IsModified = true;
    _raiseItemRenamed((T)item);
    _onItemRenamed((T)item);
  }

  public virtual string? ValidateNewItemName(ITreeItem parent, string? name) =>
    string.IsNullOrEmpty(name)
      ? "The name is empty!"
      : All.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ? $"{name} item already exists!"
        : null;

  public virtual void ItemMove(ITreeItem item, ITreeItem dest, bool aboveDest) {
    if (item.Move(dest, aboveDest)) {
      IsModified = true;
      _raiseItemMoved((T)item);
    }
  }

  public virtual void ItemDelete(ITreeItem item) {
    All.Remove((T)item);
    IsModified = true;
    _raiseItemDeleted((T)item);
    _onItemDeleted(this, (T)item);
  }

  public virtual void TreeItemDelete(ITreeItem item) {
    var items = item.Flatten().Cast<T>().ToArray();

    foreach (var treeItem in items)
      ItemDelete(treeItem);

    _raiseItemsDeleted(items);
    _onItemsDeleted(this, items);
  }

  protected override void _onItemsDeleted(object sender, IList<T> items) {
    items[0].Parent?.Items.Remove(items[0]);

    foreach (var item in items) {
      item.Parent = null;
      item.Items.Clear();
    }
  }
}