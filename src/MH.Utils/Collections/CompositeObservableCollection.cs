using MH.Utils.BaseClasses;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MH.Utils.Collections;

public sealed class CompositeObservableCollection<T> : IDisposable {
  private readonly List<ISource> _sources = [];
  private readonly Dictionary<INotifyCollectionChanged, CollectionSource> _observableSources = [];
  private readonly ObservableCollection<T> _items;

  public ObservableCollection<T> Items => _items;

  public CompositeObservableCollection() {
    _items = [];
  }

  public CompositeObservableCollection(ObservableCollection<T> items) {
    _items = items;
  }

  public CompositeObservableCollection<T> Add(T item) {
    var source = new ItemSource(item);

    _sources.Add(source);
    _items.Add(item);

    return this;
  }

  public CompositeObservableCollection<T> AddCollection<TSource>(IList<TSource> collection, Func<TSource, T> factory) {
    var source = new CollectionSource<TSource>(collection, factory);

    _sources.Add(source);

    foreach (var item in collection) {
      var generated = factory(item);

      source.GeneratedItems.Add(generated);
      _items.Add(generated);
    }

    if (collection is INotifyCollectionChanged notify) {
      _observableSources.Add(notify, source);
      notify.CollectionChanged += _onCollectionChanged;
    }

    return this;
  }

  private void _onCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
    if (sender is not INotifyCollectionChanged notify) return;
    if (!_observableSources.TryGetValue(notify, out var source)) return;

    int sourceIndex = _sources.IndexOf(source);
    if (sourceIndex < 0) return;
    int startIndex = _getStartIndex(sourceIndex);

    switch (e.Action) {
      case NotifyCollectionChangedAction.Add:
        _handleAdd(source, startIndex, e);
        break;

      case NotifyCollectionChangedAction.Remove:
        _handleRemove(source, startIndex, e);
        break;

      case NotifyCollectionChangedAction.Replace:
        _handleReplace(source, startIndex, e);
        break;

      case NotifyCollectionChangedAction.Move:
        _handleMove(source, startIndex, e);
        break;

      case NotifyCollectionChangedAction.Reset:
        _rebuild();
        break;
    }
  }

  private void _handleAdd(CollectionSource source, int startIndex, NotifyCollectionChangedEventArgs e) {
    if (e.NewItems == null) return;

    int newStartingIndex = e.NewStartingIndex;
    int insertIndex = startIndex + newStartingIndex;

    foreach (var item in e.NewItems) {
      var generated = source.Create(item);
      source.GeneratedItems.Insert(newStartingIndex++, generated);
      _items.Insert(insertIndex++, generated);
    }
  }

  private void _handleRemove(CollectionSource source, int startIndex, NotifyCollectionChangedEventArgs e) {
    if (e.OldItems == null) return;

    int removeIndex = startIndex + e.OldStartingIndex;

    for (int i = 0; i < e.OldItems.Count; i++) {
      source.GeneratedItems.RemoveAt(e.OldStartingIndex);
      _items.RemoveAt(removeIndex);
    }
  }

  private void _handleReplace(CollectionSource source, int startIndex, NotifyCollectionChangedEventArgs e) {
    if (e.NewItems == null) return;

    int replaceIndex = startIndex + e.NewStartingIndex;

    for (int i = 0; i < e.NewItems.Count; i++) {
      var generated = source.Create(e.NewItems[i]!);

      source.GeneratedItems[e.NewStartingIndex + i] = generated;
      _items[replaceIndex + i] = generated;
    }
  }

  private void _handleMove(CollectionSource source, int startIndex, NotifyCollectionChangedEventArgs e) {
    if (e.OldItems == null || e.OldItems.Count != 1) {
      _rebuild();
      return;
    }

    var item = source.GeneratedItems[e.OldStartingIndex];

    source.GeneratedItems.RemoveAt(e.OldStartingIndex);
    source.GeneratedItems.Insert(e.NewStartingIndex, item);

    _items.Move(
      startIndex + e.OldStartingIndex,
      startIndex + e.NewStartingIndex);
  }

  private int _getStartIndex(int sourceIndex) {
    int index = 0;

    for (int i = 0; i < sourceIndex; i++)
      index += _sources[i].Count;

    return index;
  }

  private void _rebuild() {
    _items.Clear();

    foreach (var source in _sources) {
      foreach (var item in source.Items)
        _items.Add(item);
    }
  }

  public void Dispose() {
    foreach (var pair in _observableSources)
      pair.Key.CollectionChanged -= _onCollectionChanged;

    _observableSources.Clear();
  }

  private interface ISource {
    int Count { get; }
    IEnumerable<T> Items { get; }
  }

  private sealed class ItemSource : ISource {
    private readonly T _item;
    
    public int Count => 1;
    public IEnumerable<T> Items { get { yield return _item; } }

    public ItemSource(T item) {
      _item = item;
    }
  }

  private abstract class CollectionSource : ISource {
    public List<T> GeneratedItems { get; } = [];
    public int Count => GeneratedItems.Count;
    public IEnumerable<T> Items => GeneratedItems;
    public abstract T Create(object sourceItem);
  }

  private sealed class CollectionSource<TSource> : CollectionSource {
    private readonly Func<TSource, T> _factory;

    public CollectionSource(IList<TSource> collection, Func<TSource, T> factory) {
      Collection = collection;
      _factory = factory;
    }

    public IList<TSource> Collection { get; }

    public override T Create(object sourceItem) {
      return _factory((TSource)sourceItem);
    }
  }
}