using MH.Utils.BaseClasses;
using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MH.Utils;

public class FlatTree {
  private readonly ObservableCollection<ITreeItem> _rootHolder;
  private readonly Dictionary<ITreeItem, int> _indexMap = new();
  private List<FlatTreeItem> _items = [];

  public IReadOnlyList<FlatTreeItem> Items => _items;

  public event Action? ResetEvent;
  public event Action<int, int>? RangeInsertedEvent;
  public event Action<int, int>? RangeRemovedEvent;
  public event Action<int>? IsExpandedChangedEvent;
  public event Action<int>? IsExpandedVisibilityChangedEvent;
  public event Action<ITreeItem, int, PropertyChangedEventArgs>? TreeItemPropertyChangedEvent;

  public FlatTree(ObservableCollection<ITreeItem> rootHolder) {
    _rootHolder = rootHolder;
    _rootHolder.CollectionChanged += (_, _) => Reset();
  }

  public void Reset() {
    Clear();
    _insertItems(_rootHolder, 0, 0, notify: false);
    ResetEvent?.Invoke();
  }

  public void Clear() {
    _unsubscribe(_items);
    _items = [];
    _indexMap.Clear();
  }

  public int IndexOf(ITreeItem item) =>
    _indexMap.TryGetValue(item, out var index) ? index : -1;

  private void _insertItems(IEnumerable<ITreeItem> items, int startLevel, int index, bool notify = true) {
    var newItems = Tree.ToFlatTreeItems(items, startLevel);
    _subscribe(newItems);
    _items.InsertRange(index, newItems);
    _rebuildIndex();
    if (notify) RangeInsertedEvent?.Invoke(index, newItems.Count);
  }

  private void _removeChildren(int parentIndex) {
    int end = _findSubtreeEndIndex(parentIndex);
    int removeStart = parentIndex + 1;
    int count = end - parentIndex - 1;
    _removeItems(removeStart, count);
  }

  private void _removeItem(int index) {
    int end = _findSubtreeEndIndex(index);
    int count = end - index;
    _removeItems(index, count);
  }

  private void _removeItems(int removeStart, int count) {
    if (count <= 0) return;

    for (int i = 0; i < count; i++)
      _unsubscribe(_items[removeStart + i].TreeItem);

    _items.RemoveRange(removeStart, count);
    _rebuildIndex();
    RangeRemovedEvent?.Invoke(removeStart, count);
  }

  private int _findSubtreeEndIndex(int parentIndex) {
    if (parentIndex < 0) return -1;
    int level = _items[parentIndex].Level;
    int i = parentIndex + 1;
    while (i < _items.Count && _items[i].Level > level) i++;
    return i;
  }

  private void _rebuildIndex() {
    _indexMap.Clear();
    for (int i = 0; i < _items.Count; i++)
      _indexMap[_items[i].TreeItem] = i;
  }

  private void _subscribe(ITreeItem item) {
    item.PropertyChanged += _onTreeItemPropertyChanged;
    item.Items.CollectionChanged += _onTreeItemsChanged;
  }

  private void _subscribe(IEnumerable<FlatTreeItem> items) {
    foreach (var item in items) _subscribe(item.TreeItem);
  }

  private void _unsubscribe(ITreeItem item) {
    item.PropertyChanged -= _onTreeItemPropertyChanged;
    item.Items.CollectionChanged -= _onTreeItemsChanged;
  }

  private void _unsubscribe(IEnumerable<FlatTreeItem> items) {
    foreach (var item in items) _unsubscribe(item.TreeItem);
  }

  private void _onTreeItemPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    var item = (ITreeItem)sender!;
    int index = IndexOf(item);
    if (index < 0) return;

    TreeItemPropertyChangedEvent?.Invoke(item, index, e);

    if (!e.Is(nameof(TreeItem.IsExpanded))) return;

    if (item.IsExpanded) {
      if (!_hasInsertedChildren(item, index))
        _insertItems(item.Items, _items[index].Level + 1, index + 1);
    }
    else
      _removeChildren(index);

    IsExpandedChangedEvent?.Invoke(index);
  }

  private bool _hasInsertedChildren(ITreeItem parent, int parentIndex) {
    int next = parentIndex + 1;
    return next < _items.Count && _items[next].TreeItem.Parent == parent;
  }

  private void _onTreeItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
    if (sender is not IHasOwner { Owner: ITreeItem parent }) return;

    int parentIndex = IndexOf(parent);
    if (parentIndex < 0) return;

    IsExpandedVisibilityChangedEvent?.Invoke(parentIndex);

    if (!parent.IsExpanded) return;
    int parentLevel = _items[parentIndex].Level;

    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
      int insertIndex = _getInsertIndexForChild(parentIndex, e.NewStartingIndex);
      var items = new List<ITreeItem>(e.NewItems.Count);
      foreach (ITreeItem item in e.NewItems) items.Add(item);
      _insertItems(items, parentLevel + 1, insertIndex);
      return;
    }

    if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
      foreach (ITreeItem item in e.OldItems)
        _removeItem(IndexOf(item));

      return;
    }

    _removeChildren(parentIndex);
    _insertItems(parent.Items, parentLevel + 1, parentIndex + 1);
  }

  private int _getInsertIndexForChild(int parentIndex, int childIndex) {
    int level = _items[parentIndex].Level;
    int i = parentIndex + 1;
    int currentChild = 0;

    while (i < _items.Count && _items[i].Level > level) {
      if (_items[i].Level == level + 1) {
        if (currentChild == childIndex) return i;
        currentChild++;
      }

      int subtreeLevel = _items[i].Level;
      i++;
      while (i < _items.Count && _items[i].Level > subtreeLevel) i++;
    }

    return i;
  }
}