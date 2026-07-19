using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MH.Utils.Tree;

public class FlatTree {
  private readonly ObservableCollection<ITreeItem> _rootHolder;
  private readonly Dictionary<ITreeItem, int> _indexMap = new();
  private readonly HashSet<ITreeItem> _subscribedItems = new();

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

    foreach (var root in _rootHolder)
      _subscribeSubtree(root);

    _items = _rootHolder.ToFlatTreeItems();

    foreach (var item in _items)
      item.HasVisibleChildren = item.TreeItem.HasVisibleChildren();

    _rebuildIndex();

    ResetEvent?.Invoke();
  }

  public void Clear() {
    foreach (var item in _subscribedItems)
      _unsubscribe(item);

    _subscribedItems.Clear();
    _items = [];
    _indexMap.Clear();
  }

  public int IndexOf(ITreeItem item) =>
    _indexMap.TryGetValue(item, out var index) ? index : -1;

  private void _insertItems(IEnumerable<ITreeItem> items, int startLevel, int index) {
    var newItems = items.ToFlatTreeItems(startLevel);

    if (newItems.Count == 0) return;

    foreach (var item in newItems)
      item.HasVisibleChildren = item.TreeItem.HasVisibleChildren();

    _items.InsertRange(index, newItems);
    _rebuildIndex();

    _updateHasVisibleChildren(newItems[0].TreeItem.Parent);
    RangeInsertedEvent?.Invoke(index, newItems.Count);
  }

  private void _removeSubtree(int index) {
    var end = _findSubtreeEndIndex(index);
    _removeItems(index, end - index);
  }

  private void _removeChildren(int parentIndex) {
    var end = _findSubtreeEndIndex(parentIndex);
    var removeStart = parentIndex + 1;
    var count = end - removeStart;
    _removeItems(removeStart, count);
  }

  private void _removeItems(int removeStart, int count) {
    if (count <= 0) return;
    var parent = _items[removeStart].TreeItem.Parent;
    _items.RemoveRange(removeStart, count);
    _rebuildIndex();
    _updateHasVisibleChildren(parent);
    RangeRemovedEvent?.Invoke(removeStart, count);
  }

  private void _updateHasVisibleChildren(ITreeItem? parent) {
    if (parent == null) return;
    var index = IndexOf(parent);
    if (index < 0) return;

    var hasVisibleChildren = parent.HasVisibleChildren();
    var flatItem = _items[index];
    if (flatItem.HasVisibleChildren == hasVisibleChildren) return;
    flatItem.HasVisibleChildren = hasVisibleChildren;
    IsExpandedVisibilityChangedEvent?.Invoke(index);
  }

  private int _findSubtreeEndIndex(int parentIndex) {
    if (parentIndex < 0) return -1;
    var level = _items[parentIndex].Level;
    var i = parentIndex + 1;
    while (i < _items.Count && _items[i].Level > level) i++;
    return i;
  }

  private void _rebuildIndex() {
    _indexMap.Clear();
    for (var i = 0; i < _items.Count; i++)
      _indexMap[_items[i].TreeItem] = i;
  }

  private void _subscribe(ITreeItem item) {
    item.PropertyChanged += _onTreeItemPropertyChanged;
    item.Items.CollectionChanged += _onTreeItemsChanged;
  }

  private void _subscribeSubtree(ITreeItem item) {
    if (!_subscribedItems.Add(item)) return;

    _subscribe(item);

    if (!item.IsExpanded) return;

    foreach (var child in item.Items)
      _subscribeSubtree(child);
  }

  private void _unsubscribe(ITreeItem item) {
    item.PropertyChanged -= _onTreeItemPropertyChanged;
    item.Items.CollectionChanged -= _onTreeItemsChanged;
  }

  private void _unsubscribeSubtree(ITreeItem item) {
    if (!_subscribedItems.Remove(item)) return;

    _unsubscribe(item);

    foreach (var child in item.Items)
      _unsubscribeSubtree(child);
  }

  private void _onTreeItemPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    var item = (ITreeItem)sender!;
    var index = IndexOf(item);

    if (index >= 0)
      TreeItemPropertyChangedEvent?.Invoke(item, index, e);

    if (e.Is(nameof(TreeItem.IsExpanded)))
      _onIsExpandedChanged(item, index);
    else if (e.Is(nameof(TreeItem.IsHidden)))
      _onIsHiddenChanged(item, index);
  }

  private void _onIsExpandedChanged(ITreeItem item, int index) {
    if (index < 0) return;

    if (item.IsExpanded) {
      foreach (var child in item.Items)
        _subscribeSubtree(child);

      _removeChildren(index);
      _insertItems(item.Items, _items[index].Level + 1, index + 1);
    }
    else {
      foreach (var child in item.Items)
        _unsubscribeSubtree(child);

      _removeChildren(index);
    }

    IsExpandedChangedEvent?.Invoke(index);
  }

  private void _onIsHiddenChanged(ITreeItem item, int index) {
    if (item.IsHidden) {
      if (index >= 0) _removeSubtree(index);
      return;
    }

    if (index >= 0) return;
    if (!item.IsVisible()) return;
    _insertItems([item], item.GetLevel(), _getInsertIndex(item));
  }

  private int _getInsertIndex(ITreeItem item) {
    if (item.Parent == null)
      return _getRootInsertIndex(item);

    var parent = item.Parent;
    var childIndex = parent.Items.IndexOf(item);

    for (var i = childIndex - 1; i >= 0; i--) {
      var visibleIndex = IndexOf(parent.Items[i]);

      if (visibleIndex >= 0)
        return _findSubtreeEndIndex(visibleIndex);
    }

    var parentIndex = IndexOf(parent);

    return parentIndex + 1;
  }

  private int _getRootInsertIndex(ITreeItem item) {
    var rootIndex = _rootHolder.IndexOf(item);

    for (var i = rootIndex - 1; i >= 0; i--) {
      var visibleIndex = IndexOf(_rootHolder[i]);

      if (visibleIndex >= 0)
        return _findSubtreeEndIndex(visibleIndex);
    }

    return 0;
  }

  private void _onTreeItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
    if (sender is not IHasOwner { Owner: ITreeItem parent }) return;

    if (e.OldItems != null)
      foreach (ITreeItem item in e.OldItems)
        _unsubscribeSubtree(item);

    if (e.NewItems != null && parent.IsExpanded)
      foreach (ITreeItem item in e.NewItems)
        _subscribeSubtree(item);

    if (!parent.IsVisible()) return;

    if (!parent.IsExpanded) {
      _updateHasVisibleChildren(parent);
      return;
    }

    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null) {
      var insertIndex = _getInsertIndex((ITreeItem)e.NewItems[0]!);

      var items = new List<ITreeItem>(e.NewItems.Count);

      foreach (ITreeItem item in e.NewItems)
        items.Add(item);

      _insertItems(items, parent.GetLevel() + 1, insertIndex);

      return;
    }

    if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null) {
      foreach (ITreeItem item in e.OldItems) {
        var index = IndexOf(item);

        if (index >= 0)
          _removeSubtree(index);
      }

      return;
    }

    Reset();
  }
}