using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public static class BindingU {
  private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertySubscriptionTable> _propertySubs = new();
  private static readonly ConditionalWeakTable<INotifyCollectionChanged, CollectionSubscriptionTable> _collectionSubs = new();

  public static TTarget WithBind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TProp> getter,
    Action<TTarget, TProp> onChange)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    target.Bind(source, propertyName, getter, onChange);
    return target;
  }

  public static IDisposable Bind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TProp> getter,
    Action<TTarget, TProp> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    var weakTarget = new WeakReference<TTarget>(target);
    var table = _propertySubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source, propertyName, o => getter((TSource)o!));

    void handler(object? valueObj) {
      if (!weakTarget.TryGetTarget(out var t)) {
        sub.RemoveHandler(handler);
        return;
      }

      onChange(t, (TProp)valueObj!);
    }

    if (invokeInitOnChange)
      onChange(target, getter(source));

    return sub.AddHandler(handler);
  }

  public static IDisposable Bind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, TProp?> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    return _bindNested(target, source, propertyNames, getters, (t, v) => onChange(t, (TProp?)v), null, invokeInitOnChange);
  }

  public static IDisposable Bind<TTarget, TCol>(
    this TTarget target,
    TCol source,
    Action<TTarget, TCol, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TCol : INotifyCollectionChanged {

    var weakTarget = new WeakReference<TTarget>(target);
    return _bindCollection(target, weakTarget, source, onChange, invokeInitOnChange);
  }

  public static IDisposable Bind<TTarget, TSource, TCol>(
    this TTarget target,
    TSource source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged
    where TCol : class, INotifyCollectionChanged {

    return _bindNested(target, source, propertyNames, getters, null, (t, c, e) => onChange(t, (TCol?)c, e), invokeInitOnChange);
  }

  public static IDisposable Bind<TTarget, TSource, TCol>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TCol?> getter,
    Action<TTarget, TCol, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged
    where TCol : INotifyCollectionChanged {

    var weakTarget = new WeakReference<TTarget>(target);
    var subs = new List<IDisposable>();
    bool disposed = false;

    void Rebind() {
      if (disposed || !weakTarget.TryGetTarget(out var strongTarget)) return;

      for (int i = subs.Count - 1; i >= 1; i--) {
        subs[i].Dispose();
        subs.RemoveAt(i);
      }

      if (getter(source) is not TCol instance) return;

      subs.Add(_bindCollection(strongTarget, weakTarget, instance, onChange, invokeInitOnChange));
    }

    var table = _propertySubs.GetOrCreateValue(source);
    var rootSub = table.GetOrAdd(source, propertyName, o => getter((TSource)o!));
    var handler = rootSub.AddHandler(_ => { if (weakTarget.TryGetTarget(out var _)) Rebind(); });
    subs.Add(handler);
    Rebind();

    return new NestedDispose(subs);
  }

  private static IDisposable _bindCollection<TTarget, TCol>(
    TTarget target,
    WeakReference<TTarget> weakTarget,
    TCol collection,
    Action<TTarget, TCol, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange)
    where TTarget : class
    where TCol : INotifyCollectionChanged {

    var table = _collectionSubs.GetOrCreateValue(collection);
    var collSub = table.GetOrAdd(collection);

    void Handler(object? s, NotifyCollectionChangedEventArgs e) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, collection, e);
      else
        collSub.RemoveHandler(Handler);
    }

    var handler = collSub.AddHandler(Handler);

    if (invokeInitOnChange)
      onChange(target, collection, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    return handler;
  }

  private static IDisposable _bindNested<TTarget>(
    TTarget target,
    INotifyPropertyChanged root,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, object?>? onChangeProperty,
    Action<TTarget, INotifyCollectionChanged, NotifyCollectionChangedEventArgs>? onChangeCollection,
    bool invokeInitOnChange)
    where TTarget : class {

    int hopCount = propertyNames.Length;
    var subs = new List<IDisposable>();
    var weakTarget = new WeakReference<TTarget>(target);
    bool disposed = false;

    void RebuildFrom(int startHop) {
      if (disposed || !weakTarget.TryGetTarget(out _)) return;

      for (int i = subs.Count - 1; i >= startHop; i--) {
        subs[i].Dispose();
        subs.RemoveAt(i);
      }

      object? currentInstance = root;

      for (int i = 0; i < startHop; i++)
        currentInstance = currentInstance != null ? getters[i](currentInstance) : null;

      for (int hop = startHop; hop < hopCount; hop++) {
        if (currentInstance == null) break;

        var propertyName = propertyNames[hop];
        var capturedHop = hop;

        if (hop < hopCount - 1 && currentInstance is not INotifyPropertyChanged)
          throw new InvalidOperationException($"Property '{propertyName}' of '{currentInstance.GetType()}' must implement INotifyPropertyChanged.");

        if (currentInstance is INotifyPropertyChanged npc) {
          var table = _propertySubs.GetOrCreateValue(npc);
          var sub = table.GetOrAdd(npc, propertyName, o => getters[capturedHop](o));
          var handler = sub.AddHandler(_ => {
            if (weakTarget.TryGetTarget(out var _))
              RebuildFrom(capturedHop + 1);
          });
          subs.Add(handler);
        }

        currentInstance = getters[hop](currentInstance);
      }

      if (!weakTarget.TryGetTarget(out var strongTarget)) return;

      if (onChangeCollection != null && currentInstance is INotifyCollectionChanged collection)
        subs.Add(_bindCollection(strongTarget, weakTarget, collection, onChangeCollection, invokeInitOnChange));
      else if (onChangeProperty != null && invokeInitOnChange)
        onChangeProperty(strongTarget, currentInstance);
    }

    RebuildFrom(0);

    return new NestedDispose(subs);
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
    void RemoveAllHandlers();
  }

  private class PropertySubscription : IPropertySubscription {
    public string PropertyName { get; }

    private readonly List<Action<object?>> _handlers = new();
    private readonly INotifyPropertyChanged _source;
    private readonly Func<object?, object?> _getter;

    public PropertySubscription(INotifyPropertyChanged source, string propertyName, Func<object?, object?> getter) {
      _source = source;
      PropertyName = propertyName;
      _getter = getter;
      _source.PropertyChanged += OnChanged;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) {
      if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == PropertyName) {
        var value = _getter(_source);
        foreach (var h in _handlers.ToArray())
          h(value);
      }
    }

    public IDisposable AddHandler(Action<object?> handler) {
      _handlers.Add(handler);
      return new HandlerWrapper(this, handler);
    }

    public void RemoveHandler(Action<object?> handler) {
      _handlers.Remove(handler);
    }

    public void RemoveAllHandlers() {
      _handlers.Clear();
    }

    private sealed class HandlerWrapper : IDisposable {
      private readonly PropertySubscription _parent;
      private readonly Action<object?> _handler;
      private bool _disposed;

      public HandlerWrapper(PropertySubscription parent, Action<object?> handler) {
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
    private readonly List<IPropertySubscription> _subs = new();

    public PropertySubscription GetOrAdd(INotifyPropertyChanged source, string propertyName, Func<object?, object?> getter) {
      foreach (var sub in _subs) {
        if (sub.PropertyName == propertyName && sub is PropertySubscription typed)
          return typed;
      }

      var newSub = new PropertySubscription(source, propertyName, getter);
      _subs.Add(newSub);
      return newSub;
    }

    public void Remove(IPropertySubscription sub) {
      _subs.Remove(sub);
      sub.RemoveAllHandlers();
    }
  }

  private interface ICollectionSubscription {
    INotifyCollectionChanged Source { get; }
    void RemoveAllHandlers();
  }

  private sealed class CollectionSubscription : ICollectionSubscription {
    private readonly List<NotifyCollectionChangedEventHandler> _handlers = new();

    public INotifyCollectionChanged Source { get; }

    public CollectionSubscription(INotifyCollectionChanged source) {
      Source = source;
      Source.CollectionChanged += OnChanged;
    }

    private void OnChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      foreach (var h in _handlers.ToArray())
        h(sender, e);
    }

    public IDisposable AddHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Add(handler);
      return new HandlerWrapper(this, handler);
    }

    public void RemoveHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Remove(handler);
    }

    public void RemoveAllHandlers() {
      _handlers.Clear();
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

    public void Remove(ICollectionSubscription sub) {
      _subs.Remove(sub);
      sub.RemoveAllHandlers();
    }
  }
}