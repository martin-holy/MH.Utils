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
  private static readonly ConditionalWeakTable<INotifyCollectionChanged, CollectionSubscriptionTable2> _collectionSubs2 = new();

  public static string GetPropertyName<TSource, TProp>(Expression<Func<TSource, TProp>> propertyExpression) {
    if (propertyExpression.Body is not MemberExpression m)
      throw new ArgumentException("Expression must be a property access", nameof(propertyExpression));

    return m.Member.Name;
  }

  [Obsolete]
  public static IDisposable Bind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression,
    Action<TTarget, TProp> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    var propertyName = GetPropertyName(propertyExpression);
    var getter = GetterCache.GetGetter<TSource, TProp>(propertyName);
    var weakTarget = new WeakReference<TTarget>(target);
    var table = _propertySubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source, propertyName, getter);

    void handler(TProp value) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, value);
      else
        sub.RemoveHandler(handler);
    }

    if (invokeInitOnChange)
      onChange(target, getter(source));

    return sub.AddHandler(handler);
  }

  [Obsolete] // each bindind should have a source to watch over changes on collection property it self
  public static IDisposable Bind<TTarget>(
    this TTarget target,
    INotifyCollectionChanged source,
    Action<TTarget, NotifyCollectionChangedEventArgs> onChange)
    where TTarget : class {

    var weakTarget = new WeakReference<TTarget>(target);

    var table = _collectionSubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source);

    void handler(object? s, NotifyCollectionChangedEventArgs e) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, e);
      else
        sub.RemoveHandler(handler);
    }

    onChange(target, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    return sub.AddHandler(handler);
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
    var table = _propertySubs2.GetOrCreateValue(source);
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
    Action<TTarget, TProp> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    return _bindNested(target, source, propertyNames, getters,
      onLeafValue: (t, v) => onChange(t, (TProp)v!),
      onLeafCollection: null,
      invokeInit: invokeInitOnChange);
  }

  public static IDisposable Bind<TTarget, TSource, TCol>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TCol?> getter,
    Action<TTarget, TCol, NotifyCollectionChangedEventArgs> onLeafCollection,
    bool invokeInit = true)
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

      subs.Add(_bindCollection(strongTarget, weakTarget, instance, onLeafCollection, invokeInit));
    }

    var table = _propertySubs2.GetOrCreateValue(source);
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
    Action<TTarget, TCol, NotifyCollectionChangedEventArgs> onLeafCollection,
    bool invokeInit)
    where TTarget : class
    where TCol : INotifyCollectionChanged {

    var table = _collectionSubs2.GetOrCreateValue(collection);
    var collSub = table.GetOrAdd(collection);

    void Handler(object? s, NotifyCollectionChangedEventArgs e) {
      if (weakTarget.TryGetTarget(out var t))
        onLeafCollection(t, collection, e);
      else
        collSub.RemoveHandler(Handler);
    }

    var handler = collSub.AddHandler(Handler);

    if (invokeInit)
      onLeafCollection(target, collection, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    return handler;
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

    return _bindNested(target, source, propertyNames, getters,
      onLeafValue: null,
      onLeafCollection: (t, c, e) => onChange(t, (TCol?)c, e),
      invokeInit: invokeInitOnChange);
  }

  private static IDisposable _bindNested<TTarget>(
    TTarget target,
    INotifyPropertyChanged root,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, object?>? onLeafValue,
    Action<TTarget, INotifyCollectionChanged, NotifyCollectionChangedEventArgs>? onLeafCollection,
    bool invokeInit)
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
          var table = _propertySubs2.GetOrCreateValue(npc);
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

      if (onLeafCollection != null && currentInstance is INotifyCollectionChanged collection)
        subs.Add(_bindCollection(strongTarget, weakTarget, collection, onLeafCollection, invokeInit));
      else if (onLeafValue != null && invokeInit)
        onLeafValue(strongTarget, currentInstance!);
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

  private sealed class HandlerSubscription<TSource, TProp> : IDisposable
    where TSource : class, INotifyPropertyChanged {
    private readonly PropertySubscription<TSource, TProp> _parent;
    private readonly Action<TProp> _handler;
    private bool _disposed;

    public HandlerSubscription(PropertySubscription<TSource, TProp> parent, Action<TProp> handler) {
      _parent = parent;
      _handler = handler;
    }

    public void Dispose() {
      if (_disposed) return;
      _disposed = true;
      _parent.RemoveHandler(_handler);
    }
  }

  private interface IPropertySubscription {
    string PropertyName { get; }
  }

  private class PropertySubscription<TSource, TProp> : IPropertySubscription
    where TSource : class, INotifyPropertyChanged {

    private readonly TSource _source;
    private readonly Func<TSource, TProp> _getter;
    private readonly PropertySubscriptionTable _table;
    private object? _handlers; // null | Action<TProp> | Action<TProp>[]

    public string PropertyName { get; }

    public PropertySubscription(TSource source, string propertyName, Func<TSource, TProp> getter, PropertySubscriptionTable table) {
      _source = source;
      PropertyName = propertyName;
      _getter = getter;
      _table = table;
      _source.PropertyChanged += _onChanged;
    }

    private void _onChanged(object? sender, PropertyChangedEventArgs e) {
      if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != PropertyName) return;

      var value = _getter(_source);
      switch (_handlers) {
        case Action<TProp> single:
          single(value);
          break;
        case Action<TProp>[] arr:
          for (int i = 0; i < arr.Length; i++)
            arr[i](value);
          break;
      }
    }

    public IDisposable AddHandler(Action<TProp> handler) {
      if (_handlers is null) {
        _handlers = handler;
      }
      else if (_handlers is Action<TProp> single) {
        _handlers = new Action<TProp>[] { single, handler };
      }
      else if (_handlers is Action<TProp>[] arr) {
        var newArr = new Action<TProp>[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[arr.Length] = handler;
        _handlers = newArr;
      }
      return new HandlerSubscription<TSource, TProp>(this, handler);
    }

    public void RemoveHandler(Action<TProp> handler) {
      if (_handlers is Action<TProp> single) {
        if (single == handler) {
          _handlers = null;
          _cleanupIfEmpty();
        }
      }
      else if (_handlers is Action<TProp>[] arr) {
        int idx = Array.IndexOf(arr, handler);
        if (idx >= 0) {
          if (arr.Length == 2) {
            // collapse back to single
            _handlers = arr[1 - idx];
          }
          else {
            var newArr = new Action<TProp>[arr.Length - 1];
            if (idx > 0) Array.Copy(arr, 0, newArr, 0, idx);
            if (idx < arr.Length - 1) Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
            _handlers = newArr.Length == 0 ? null : newArr;
          }
          if (_handlers == null)
            _cleanupIfEmpty();
        }
      }
    }

    private void _cleanupIfEmpty() {
      _source.PropertyChanged -= _onChanged;
      _table.Remove(PropertyName);
    }
  }

  private class PropertySubscriptionTable {
    private object? _subs; // null | IPropertySubscription | IPropertySubscription[]

    public PropertySubscription<TSource, TProp> GetOrAdd<TSource, TProp>(
      TSource source, string propertyName, Func<TSource, TProp> getter)
      where TSource : class, INotifyPropertyChanged {

      if (_subs is null) {
        var sub = new PropertySubscription<TSource, TProp>(source, propertyName, getter, this);
        _subs = sub;
        return sub;
      }
      else if (_subs is IPropertySubscription single) {
        if (single.PropertyName == propertyName && single is PropertySubscription<TSource, TProp> typed)
          return typed;

        // promote to array
        var newSub = new PropertySubscription<TSource, TProp>(source, propertyName, getter, this);
        _subs = new IPropertySubscription[] { single, newSub };
        return newSub;
      }
      else if (_subs is IPropertySubscription[] arr) {
        foreach (var entry in arr) {
          if (entry.PropertyName == propertyName && entry is PropertySubscription<TSource, TProp> ps)
            return ps;
        }
        var newArr = new IPropertySubscription[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        var sub = new PropertySubscription<TSource, TProp>(source, propertyName, getter, this);
        newArr[arr.Length] = sub;
        _subs = newArr;
        return sub;
      }

      throw new InvalidOperationException("Unexpected state in PropertySubscriptionTable");
    }

    public void Remove(string propertyName) {
      if (_subs is IPropertySubscription single) {
        if (single.PropertyName == propertyName)
          _subs = null;
      }
      else if (_subs is IPropertySubscription[] arr) {
        int idx = Array.FindIndex(arr, s => s.PropertyName == propertyName);
        if (idx >= 0) {
          if (arr.Length == 2) {
            // collapse back to single
            _subs = arr[1 - idx];
          }
          else {
            var newArr = new IPropertySubscription[arr.Length - 1];
            if (idx > 0) Array.Copy(arr, 0, newArr, 0, idx);
            if (idx < arr.Length - 1) Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
            _subs = newArr;
          }
        }
      }
    }
  }

  // Object-based subscription interface
  private interface IPropertySubscription2 {
    string PropertyName { get; }
    void RemoveAllHandlers(); // remove all handlers
  }

  private class PropertySubscription2 : IPropertySubscription2 {
    public string PropertyName { get; }

    private readonly List<Action<object?>> _handlers = new();
    private readonly INotifyPropertyChanged _source;
    private readonly Func<object?, object?> _getter;

    public PropertySubscription2(INotifyPropertyChanged source, string propertyName, Func<object?, object?> getter) {
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
      private readonly PropertySubscription2 _parent;
      private readonly Action<object?> _handler;
      private bool _disposed;

      public HandlerWrapper(PropertySubscription2 parent, Action<object?> handler) {
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

  private class PropertySubscriptionTable2 {
    private readonly List<IPropertySubscription2> _subs = new();

    public PropertySubscription2 GetOrAdd(INotifyPropertyChanged source, string propertyName, Func<object?, object?> getter) {
      foreach (var sub in _subs) {
        if (sub.PropertyName == propertyName && sub is PropertySubscription2 typed)
          return typed;
      }

      var newSub = new PropertySubscription2(source, propertyName, getter);
      _subs.Add(newSub);
      return newSub;
    }

    public void Remove(IPropertySubscription2 sub) {
      _subs.Remove(sub);
      sub.RemoveAllHandlers();
    }
  }

  private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertySubscriptionTable2> _propertySubs2 = new();

  private interface ICollectionSubscription2 {
    INotifyCollectionChanged Source { get; }
    void RemoveAllHandlers(); // remove all handlers
  }

  private sealed class CollectionSubscription2 : ICollectionSubscription2 {
    public INotifyCollectionChanged Source { get; }

    private readonly List<NotifyCollectionChangedEventHandler> _handlers = new();

    public CollectionSubscription2(INotifyCollectionChanged source) {
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
      private readonly CollectionSubscription2 _parent;
      private readonly NotifyCollectionChangedEventHandler _handler;
      private bool _disposed;

      public HandlerWrapper(CollectionSubscription2 parent, NotifyCollectionChangedEventHandler handler) {
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

  private class CollectionSubscriptionTable2 {
    private readonly List<ICollectionSubscription2> _subs = new();

    public CollectionSubscription2 GetOrAdd(INotifyCollectionChanged source) {
      foreach (var sub in _subs) {
        if (ReferenceEquals(sub.Source, source) && sub is CollectionSubscription2 typed)
          return typed;
      }

      var newSub = new CollectionSubscription2(source);
      _subs.Add(newSub);
      return newSub;
    }

    public void Remove(ICollectionSubscription2 sub) {
      _subs.Remove(sub);
      sub.RemoveAllHandlers();
    }
  }

  private sealed class CollectionHandlerSubscription : IDisposable {
    private readonly CollectionSubscription _parent;
    private readonly NotifyCollectionChangedEventHandler _handler;
    private bool _disposed;

    public CollectionHandlerSubscription(CollectionSubscription parent, NotifyCollectionChangedEventHandler handler) {
      _parent = parent;
      _handler = handler;
    }

    public void Dispose() {
      if (_disposed) return;
      _disposed = true;
      _parent.RemoveHandler(_handler);
    }
  }

  private class CollectionSubscription {
    private readonly INotifyCollectionChanged _source;
    private readonly CollectionSubscriptionTable _table;
    private object? _handlers; // null | NotifyCollectionChangedEventHandler | NotifyCollectionChangedEventHandler[]

    public CollectionSubscription(INotifyCollectionChanged source, CollectionSubscriptionTable table) {
      _source = source;
      _table = table;
      _source.CollectionChanged += _onChanged;
    }

    private void _onChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      switch (_handlers) {
        case NotifyCollectionChangedEventHandler single:
          single(sender, e);
          break;
        case NotifyCollectionChangedEventHandler[] arr:
          for (int i = 0; i < arr.Length; i++)
            arr[i](sender, e);
          break;
      }
    }

    public IDisposable AddHandler(NotifyCollectionChangedEventHandler handler) {
      if (_handlers is null) {
        _handlers = handler;
      }
      else if (_handlers is NotifyCollectionChangedEventHandler single) {
        _handlers = new NotifyCollectionChangedEventHandler[] { single, handler };
      }
      else if (_handlers is NotifyCollectionChangedEventHandler[] arr) {
        var newArr = new NotifyCollectionChangedEventHandler[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[arr.Length] = handler;
        _handlers = newArr;
      }
      return new CollectionHandlerSubscription(this, handler);
    }

    public void RemoveHandler(NotifyCollectionChangedEventHandler handler) {
      if (_handlers is NotifyCollectionChangedEventHandler single) {
        if (single == handler) {
          _handlers = null;
          _cleanupIfEmpty();
        }
      }
      else if (_handlers is NotifyCollectionChangedEventHandler[] arr) {
        int idx = Array.IndexOf(arr, handler);
        if (idx >= 0) {
          if (arr.Length == 2) {
            // collapse to single
            _handlers = arr[1 - idx];
          }
          else {
            var newArr = new NotifyCollectionChangedEventHandler[arr.Length - 1];
            if (idx > 0) Array.Copy(arr, 0, newArr, 0, idx);
            if (idx < arr.Length - 1) Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
            _handlers = newArr.Length == 0 ? null : newArr;
          }
          if (_handlers == null)
            _cleanupIfEmpty();
        }
      }
    }

    private void _cleanupIfEmpty() {
      _source.CollectionChanged -= _onChanged;
      _table.Clear();
    }
  }

  private class CollectionSubscriptionTable {
    private object? _subs; // null | CollectionSubscription | CollectionSubscription[]

    public CollectionSubscription GetOrAdd(INotifyCollectionChanged source) {
      if (_subs is null) {
        var sub = new CollectionSubscription(source, this);
        _subs = sub;
        return sub;
      }
      else if (_subs is CollectionSubscription single) {
        // promote to array
        var newSub = new CollectionSubscription(source, this);
        _subs = new CollectionSubscription[] { single, newSub };
        return newSub;
      }
      else if (_subs is CollectionSubscription[] arr) {
        var newArr = new CollectionSubscription[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        var sub = new CollectionSubscription(source, this);
        newArr[arr.Length] = sub;
        _subs = newArr;
        return sub;
      }

      throw new InvalidOperationException("Unexpected state in CollectionSubscriptionTable");
    }

    public void Clear() {
      if (_subs is CollectionSubscription single)
        _subs = null;
      else if (_subs is CollectionSubscription[] arr && arr.Length == 1)
        _subs = null;
      else if (_subs is CollectionSubscription[] arr2 && arr2.Length > 1)
        _subs = arr2[..^1]; // remove last
    }
  }

  // TODO pass PropertyInfo instead of propertyName and use it as key
  public static class GetterCache {
    private static readonly Dictionary<(Type, string), Delegate> _cache = [];

    public static Func<TSource, TProp> GetGetter<TSource, TProp>(string propertyName) {
      var key = (typeof(TSource), propertyName);
      if (_cache.TryGetValue(key, out var existing))
        return (Func<TSource, TProp>)existing;

      var srcParam = Expression.Parameter(typeof(TSource), "src");
      var prop = Expression.Property(srcParam, propertyName);
      var lambda = Expression.Lambda<Func<TSource, TProp>>(prop, srcParam);
      var compiled = lambda.Compile();

      _cache[key] = compiled;
      return compiled;
    }
  }

  public static class SetterCache {
    private static readonly Dictionary<(Type, string), Delegate> _cache = [];

    public static Action<TSource, TProp> GetSetter<TSource, TProp>(string propertyName) {
      var key = (typeof(TSource), propertyName);
      if (_cache.TryGetValue(key, out var existing))
        return (Action<TSource, TProp>)existing;

      var srcParam = Expression.Parameter(typeof(TSource), "src");
      var valueParam = Expression.Parameter(typeof(TProp), "value");
      var prop = Expression.Property(srcParam, propertyName);
      var assign = Expression.Assign(prop, valueParam);
      var lambda = Expression.Lambda<Action<TSource, TProp>>(assign, srcParam, valueParam);
      var compiled = lambda.Compile();

      _cache[key] = compiled;
      return compiled;
    }
  }
}