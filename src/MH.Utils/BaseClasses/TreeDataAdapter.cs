﻿using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class TreeDataAdapter<T>(SimpleDB db, string name, int propsCount)
  : TableDataAdapter<T>(db, name, propsCount), ITreeDataAdapter<T>
  where T : class, ITreeItem {

  public event EventHandler<T>? ItemRenamedEvent;

  public virtual T ItemCreate(ITreeItem parent, string name) => throw new NotImplementedException();
  public virtual void ItemCopy(ITreeItem item, ITreeItem dest) => throw new NotImplementedException();
  
  protected void _raiseItemRenamed(T item) => ItemRenamedEvent?.Invoke(this, item);

  protected virtual void _onItemRenamed(T item) { }

  public virtual T TreeItemCreate(T item) {
    if (item.Parent != null)
      Tree.SetInOrder(item.Parent.Items, item, x => x.Name);

    return ItemCreate(item);
  }

  public virtual void ItemRename(ITreeItem item, string name) {
    item.Name = name;

    if (item.Parent != null)
      Tree.SetInOrder(item.Parent.Items, item, x => x.Name);
    
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
    if (Tree.ItemMove(item, dest, aboveDest))
      IsModified = true;
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

  protected void _linkTree(ITreeItem root, int index) {
    foreach (var (item, csv) in _allCsv.Where(x => x.Item1.Parent == null)) {
      item.Parent = string.IsNullOrEmpty(csv[index])
        ? root
        : AllDict[int.Parse(csv[index])];
      item.Parent.Items.Add(item);
    }
  }
}