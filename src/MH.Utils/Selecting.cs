﻿using MH.Utils.BaseClasses;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MH.Utils;

public class Selecting<T> : ObservableObject where T : class, ISelectable {
  public ObservableCollection<T> Items { get; } = [];

  public event EventHandler<T[]>? ItemsChangedEvent;
  public event EventHandler? AllDeselectedEvent;

  private void _raiseItemsChanged() => ItemsChangedEvent?.Invoke(this, Items.ToArray());
  private void _raiseAllDeselected() => AllDeselectedEvent?.Invoke(this, EventArgs.Empty);

  public bool Set(T item, bool value) {
    if (item.IsSelected == value) return false;

    item.IsSelected = value;

    if (value && !Items.Contains(item)) {
      Items.Add(item);
      return true;
    }

    if (!value && Items.Contains(item)) {
      Items.Remove(item);
      return true;
    }

    return false;
  }

  public bool Set(IEnumerable<T> items, bool value) {
    var change = false;

    foreach (var item in items)
      if (Set(item, value))
        change = true;

    return change;
  }

  public void Set(IList<T> items) {
    if (Set(Items.Except(items), false) || Set(items.Except(Items), true))
      _raiseItemsChanged();
  }

  public void Add(IEnumerable<T> items) {
    if (Set(items.Except(Items), true))
      _raiseItemsChanged();
  }

  public void DeselectAll() {
    if (Items.Count == 0) return;

    foreach (var item in Items)
      item.IsSelected = false;

    Items.Clear();
    _raiseAllDeselected();
  }

  public void Select(T item) =>
    Select(null, item, false, false);

  public void Select(List<T>? items, T item, bool isCtrlOn, bool isShiftOn) {
    // single select
    if (!isCtrlOn && !isShiftOn) {
      DeselectAll();

      if (Set(item, true))
        _raiseItemsChanged();

      return;
    }

    // single invert select
    if (isCtrlOn) {
      if (Set(item, !item.IsSelected))
        _raiseItemsChanged();

      return;
    }

    if (items == null) return;

    // multi select
    var indexOfItem = items.IndexOf(item);
    var fromItem = items.FirstOrDefault(x => x.IsSelected && !ReferenceEquals(x, item));
    var from = fromItem == null ? 0 : items.IndexOf(fromItem);
    var to = indexOfItem;
    var change = false;

    if (from > to) {
      to = from;
      from = indexOfItem;
    }

    for (var i = from; i < to + 1; i++)
      if (Set(items[i], true))
        change = true;

    if (change)
      _raiseItemsChanged();
  }

  public static TI? GetNotSelectedItem<TI>(IList<TI>? items, TI? item) where TI : ISelectable {
    if (items == null || item == null) return default;
    if (!item.IsSelected) return item;
    var index = items.IndexOf(item);
    if (index < 0) return default;

    for (var i = index + 1; i < items.Count; i++)
      if (!items[i].IsSelected)
        return items[i];

    for (var i = index - 1; i > -1; i--)
      if (!items[i].IsSelected)
        return items[i];

    return default;
  }
}