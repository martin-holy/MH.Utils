using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MH.Utils.BaseClasses;

public class ExtObservableCollection<T> : ObservableCollection<T>, IHasOwner {
  public object? Owner { get; init; }

  public ExtObservableCollection() { }

  public ExtObservableCollection(object? owner) => Owner = owner;

  public ExtObservableCollection(IEnumerable<T> items) : base(items) { }

  public ExtObservableCollection(IEnumerable<T> items, object? owner = null) : base(items) => Owner = owner;

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

  public void RemoveItems(IList<T> items, Action<T>? itemAction) {
    foreach (var item in items) {
      itemAction?.Invoke(item);
      Items.Remove(item);
    }

    _notifyChange(NotifyCollectionChangedAction.Remove, items);
  }

  private void _notifyChange(NotifyCollectionChangedAction action, IList<T>? items) {
    OnPropertyChanged(new("Count"));
    OnPropertyChanged(new("Item[]"));

    if (items?.Count == 1)
      OnCollectionChanged(new(action, items[0]));
    else
      OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
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