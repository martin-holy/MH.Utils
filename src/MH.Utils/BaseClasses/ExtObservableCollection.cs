﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MH.Utils.BaseClasses;

public class ExtObservableCollection<T> : ObservableCollection<T> {
  public ExtObservableCollection() { }

  public ExtObservableCollection(IEnumerable<T> items) : base(items) { }

  public void Execute(Action<IList<T>> itemsAction) {
    itemsAction(Items);
    _notifyChange(NotifyCollectionChangedAction.Reset, null);
  }

  public void AddItems(IList<T>? items, Action<T>? itemAction) {
    if (items == null) return;
    foreach (var item in items) {
      itemAction?.Invoke(item);
      Items.Add(item);
    }

    _notifyChange(NotifyCollectionChangedAction.Add, items);
  }

  public void RemoveItems(IList<T> items, Action<T> itemAction) {
    foreach (var item in items) {
      itemAction?.Invoke(item);
      Items.Remove(item);
    }

    _notifyChange(NotifyCollectionChangedAction.Remove, items);
  }

  private void _notifyChange(NotifyCollectionChangedAction action, IList<T>? items) {
    OnPropertyChanged(new("Count"));
    OnPropertyChanged(new("Item[]"));

    if (items == null)
      OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    else
      OnCollectionChanged(new(action, items));
  }
}

public static class ExtObservableCollectionExtensions {
  public static ExtObservableCollection<T>? Toggle<T>(this ExtObservableCollection<T>? collection, T item,
    bool nullIfEmpty) {
    if (collection == null) {
      collection = [item];
      return collection;
    }

    if (!collection.Remove(item))
      collection.Add(item);

    if (nullIfEmpty && collection.Count == 0)
      collection = null;

    return collection;
  }
}