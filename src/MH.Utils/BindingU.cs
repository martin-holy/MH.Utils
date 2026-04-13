using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public static class BindingU {
  private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertySubscriptionTable> _propertySubs = new();
  private static readonly ConditionalWeakTable<INotifyCollectionChanged, CollectionSubscriptionTable> _collectionSubs = new();

  public static IDisposable Bind<TSource, TProp>(
    this TSource source,
    string propertyName,
    Func<TSource, TProp?> getter,
    Action<TProp?> onChange,
    bool invokeInitOnChange = true)
    where TSource : INotifyPropertyChanged {

    if (invokeInitOnChange)
      onChange(getter(source));

    var table = _propertySubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source, propertyName);

    void _handler() {
      onChange(getter(source));
    }

    return sub.AddHandler(_handler);
  }

  public static IDisposable Bind<TProp>(
    this INotifyPropertyChanged source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TProp?> onChange,
    bool invokeInitOnChange = true) {

    if (invokeInitOnChange)
      onChange((TProp?)_getInstanceAtDepth(source, getters, getters.Length));

    return _bindNested(source, propertyNames, getters, v => onChange((TProp?)v), null);
  }

  public static IDisposable Bind<TCol>(
    this TCol source,
    Action<TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TCol : INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange(source, new(NotifyCollectionChangedAction.Reset));

    return _bindCollection(source, onChange);
  }

  public static IDisposable Bind<TCol>(
    this INotifyPropertyChanged source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TCol : INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange((TCol?)_getInstanceAtDepth(source, getters, getters.Length), new(NotifyCollectionChangedAction.Reset));

    return _bindNested(source, propertyNames, getters, null, (c, e) => onChange((TCol?)c, e));
  }

  public static IDisposable Bind<TSource, TCol>(
    this TSource source,
    string propertyName,
    Func<TSource, TCol?> getter,
    Action<TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TSource : class, INotifyPropertyChanged
    where TCol : INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange((TCol?)getter(source), new(NotifyCollectionChangedAction.Reset));

    var table = _propertySubs.GetOrCreateValue(source);
    var sourceSub = table.GetOrAdd(source, propertyName);
    var subs = new List<IDisposable>(2);
    var handler = sourceSub.AddHandler(() => _rebind(false));
    subs.Add(handler);
    _rebind(true);

    void _rebind(bool initialBind) {
      if (subs.Count == 2) {
        subs[1].Dispose();
        subs.RemoveAt(1);
      }

      var instance = (TCol?)getter(source);

      if (!initialBind)
        onChange(instance, new(NotifyCollectionChangedAction.Reset));

      if (instance != null)
        subs.Add(_bindCollection(instance, onChange));
    }

    return new NestedDispose(subs);
  }

  private static IDisposable _bindCollection<TCol>(
    TCol collection,
    Action<TCol?, NotifyCollectionChangedEventArgs> onChange)
    where TCol : INotifyCollectionChanged {

    var table = _collectionSubs.GetOrCreateValue(collection);
    var collSub = table.GetOrAdd(collection);

    void _handler(object? s, NotifyCollectionChangedEventArgs e) {
      onChange(collection, e);
    }

    return collSub.AddHandler(_handler);
  }

  private static IDisposable _bindNested(
    INotifyPropertyChanged root,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<object?>? onChangeProperty,
    Action<INotifyCollectionChanged?, NotifyCollectionChangedEventArgs>? onChangeCollection) {

    int hopCount = propertyNames.Length;
    var subs = new List<IDisposable>();
    _rebuildFrom(0);

    void _rebuildFrom(int startHop) {
      for (int i = subs.Count - 1; i >= startHop; i--) {
        subs[i].Dispose();
        subs.RemoveAt(i);
      }

      object? currentInstance = _getInstanceAtDepth(root, getters, startHop);

      for (int hop = startHop; hop < hopCount; hop++) {
        if (currentInstance == null) break;

        var propertyName = propertyNames[hop];
        var capturedHop = hop;

        if (currentInstance is INotifyPropertyChanged npc) {
          var table = _propertySubs.GetOrCreateValue(npc);
          var sub = table.GetOrAdd(npc, propertyName);
          var handler = sub.AddHandler(() => _rebuildFrom(capturedHop + 1));
          subs.Add(handler);
        }
        else if (hop < hopCount - 1)
          throw new InvalidOperationException($"Property '{propertyName}' of '{currentInstance.GetType()}' must implement INotifyPropertyChanged.");

        currentInstance = getters[hop](currentInstance);
      }

      if (startHop > 0) { // not the initial RebuildFrom(0)
        if (onChangeCollection != null)
          onChangeCollection((INotifyCollectionChanged?)currentInstance, new(NotifyCollectionChangedAction.Reset));
        else if (onChangeProperty != null)
          onChangeProperty(currentInstance);
      }

      if (onChangeCollection != null && currentInstance is INotifyCollectionChanged collection)
        subs.Add(_bindCollection(collection, onChangeCollection));
    }

    return new NestedDispose(subs);
  }

  private static object? _getInstanceAtDepth(object source, Func<object?, object?>[] getters, int depth) {
    object? value = source;

    for (int i = 0; i < depth; i++)
      value = value != null ? getters[i](value) : null;

    return value;
  }

  private sealed class NestedDispose : IDisposable {
    private List<IDisposable>? _subscriptions;
    private bool _disposed;

    public NestedDispose(List<IDisposable> subscriptions) {
      _subscriptions = subscriptions;
    }

    public void Dispose() {
      if (_disposed) return;
      _disposed = true;

      var subs = _subscriptions;
      _subscriptions = null;

      if (subs is null) return;
      foreach (var sub in subs)
        sub.Dispose();
    }
  }

  private interface IPropertySubscription {
    string PropertyName { get; }
  }

  private class PropertySubscription : IPropertySubscription {
    private readonly List<Action> _handlers = new();
    private readonly INotifyPropertyChanged _source;

    public string PropertyName { get; }

    public PropertySubscription(INotifyPropertyChanged source, string propertyName) {
      _source = source;
      PropertyName = propertyName;
      _source.PropertyChanged += _onChanged;
    }

    private void _onChanged(object? sender, PropertyChangedEventArgs e) {
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == PropertyName) {
        for (int i = 0; i < _handlers.Count; i++)
          _handlers[i]();
      }
    }

    public IDisposable AddHandler(Action handler) {
      _handlers.Add(handler);
      return new HandlerWrapper(this, handler);
    }

    public void RemoveHandler(Action handler) {
      _handlers.Remove(handler);
    }

    private sealed class HandlerWrapper : IDisposable {
      private readonly PropertySubscription _parent;
      private readonly Action _handler;
      private bool _disposed;

      public HandlerWrapper(PropertySubscription parent, Action handler) {
        _parent = parent;
        _handler = handler;
      }

      public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _parent.RemoveHandler(_handler);
      }
    }
  }

  private class PropertySubscriptionTable {
    private readonly List<PropertySubscription> _subs = new();

    public PropertySubscription GetOrAdd(INotifyPropertyChanged source, string propertyName) {
      foreach (var sub in _subs) {
        if (sub.PropertyName == propertyName)
          return sub;
      }

      var newSub = new PropertySubscription(source, propertyName);
      _subs.Add(newSub);
      return newSub;
    }
  }

  private interface ICollectionSubscription {
    INotifyCollectionChanged Source { get; }
  }

  private sealed class CollectionSubscription : ICollectionSubscription {
    private readonly List<NotifyCollectionChangedEventHandler> _handlers = new();

    public INotifyCollectionChanged Source { get; }

    public CollectionSubscription(INotifyCollectionChanged source) {
      Source = source;
      Source.CollectionChanged += _onChanged;
    }

    private void _onChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      for (int i = 0; i < _handlers.Count; i++)
        _handlers[i](sender, e);
    }

    public IDisposable AddHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Add(handler);
      return new HandlerWrapper(this, handler);
    }

    public void RemoveHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Remove(handler);
    }

    private sealed class HandlerWrapper : IDisposable {
      private readonly CollectionSubscription _parent;
      private readonly NotifyCollectionChangedEventHandler _handler;
      private bool _disposed;

      public HandlerWrapper(CollectionSubscription parent, NotifyCollectionChangedEventHandler handler) {
        _parent = parent;
        _handler = handler;
      }

      public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _parent.RemoveHandler(_handler);
      }
    }
  }

  private class CollectionSubscriptionTable {
    private readonly List<ICollectionSubscription> _subs = new();

    public CollectionSubscription GetOrAdd(INotifyCollectionChanged source) {
      foreach (var sub in _subs) {
        if (ReferenceEquals(sub.Source, source) && sub is CollectionSubscription typed)
          return typed;
      }

      var newSub = new CollectionSubscription(source);
      _subs.Add(newSub);
      return newSub;
    }
  }
}