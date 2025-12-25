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
    Func<TSource, TProp?> getter,
    Action<TTarget, TProp?> onChange)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    target.Bind(source, propertyName, getter, onChange);
    return target;
  }

  public static IDisposable Bind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TProp?> getter,
    Action<TTarget, TProp?> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    if (invokeInitOnChange)
      onChange(target, getter(source));

    var weakTarget = new WeakReference<TTarget>(target);
    var table = _propertySubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source, propertyName, o => getter((TSource)o!));

    void _handler(object? value) {
      if (!weakTarget.TryGetTarget(out var t)) {
        sub.RemoveHandler(_handler);
        return;
      }

      onChange(t, (TProp?)value);
    }

    return sub.AddHandler(_handler);
  }

  public static IDisposable Bind<TTarget, TProp>(
    this TTarget target,
    INotifyPropertyChanged source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, TProp?> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class {

    if (invokeInitOnChange)
      onChange(target, (TProp?)_getInstanceAtDepth(source, getters, getters.Length));

    return _bindNested(target, source, propertyNames, getters, (t, v) => onChange(t, (TProp?)v), null);
  }

  public static IDisposable Bind<TTarget, TCol>(
    this TTarget target,
    TCol source,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TCol : INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange(target, source, new(NotifyCollectionChangedAction.Reset));

    return _bindCollection(target, new WeakReference<TTarget>(target), source, onChange);
  }

  public static IDisposable Bind<TTarget, TCol>(
    this TTarget target,
    INotifyPropertyChanged source,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TCol : class, INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange(target, (TCol?)_getInstanceAtDepth(source, getters, getters.Length), new(NotifyCollectionChangedAction.Reset));

    return _bindNested(target, source, propertyNames, getters, null, (t, c, e) => onChange(t, (TCol?)c, e));
  }

  public static IDisposable Bind<TTarget, TSource, TCol>(
    this TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TCol?> getter,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged
    where TCol : INotifyCollectionChanged {

    if (invokeInitOnChange)
      onChange(target, (TCol?)getter(source), new(NotifyCollectionChangedAction.Reset));

    var weakTarget = new WeakReference<TTarget>(target);
    var table = _propertySubs.GetOrCreateValue(source);
    var sourceSub = table.GetOrAdd(source, propertyName, o => getter((TSource)o!));
    var subs = new List<IDisposable>(2);
    var handler = sourceSub.AddHandler(_ => _rebind(false));
    subs.Add(handler);
    _rebind(true);

    void _rebind(bool initialBind) {
      if (!weakTarget.TryGetTarget(out var strongTarget)) {
        foreach (var sub in subs) sub.Dispose();
        return;
      }

      if (subs.Count == 2) {
        subs[1].Dispose();
        subs.RemoveAt(1);
      }

      var instance = (TCol?)getter(source);

      if (!initialBind)
        onChange(strongTarget, instance, new(NotifyCollectionChangedAction.Reset));

      if (instance != null)
        subs.Add(_bindCollection(strongTarget, weakTarget, instance, onChange));
    }

    return new NestedDispose(subs);
  }

  private static IDisposable _bindCollection<TTarget, TCol>(
    TTarget target,
    WeakReference<TTarget> weakTarget,
    TCol collection,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange)
    where TTarget : class
    where TCol : INotifyCollectionChanged {

    var table = _collectionSubs.GetOrCreateValue(collection);
    var collSub = table.GetOrAdd(collection);

    void _handler(object? s, NotifyCollectionChangedEventArgs e) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, collection, e);
      else
        collSub.RemoveHandler(_handler);
    }

    return collSub.AddHandler(_handler);
  }

  private static IDisposable _bindNested<TTarget>(
    TTarget target,
    INotifyPropertyChanged root,
    string[] propertyNames,
    Func<object?, object?>[] getters,
    Action<TTarget, object?>? onChangeProperty,
    Action<TTarget, INotifyCollectionChanged?, NotifyCollectionChangedEventArgs>? onChangeCollection)
    where TTarget : class {

    int hopCount = propertyNames.Length;
    var subs = new List<IDisposable>();
    var weakTarget = new WeakReference<TTarget>(target);
    _rebuildFrom(0);

    void _rebuildFrom(int startHop) {
      if (!weakTarget.TryGetTarget(out var strongTarget)) {
        foreach (var sub in subs) sub.Dispose();
        return;
      }

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
          var sub = table.GetOrAdd(npc, propertyName, o => getters[capturedHop](o));
          var handler = sub.AddHandler(_ => _rebuildFrom(capturedHop + 1));
          subs.Add(handler);
        }
        else if (hop < hopCount - 1)
          throw new InvalidOperationException($"Property '{propertyName}' of '{currentInstance.GetType()}' must implement INotifyPropertyChanged.");

        currentInstance = getters[hop](currentInstance);
      }

      if (startHop > 0) { // not the initial RebuildFrom(0)
        if (onChangeCollection != null)
          onChangeCollection(strongTarget, (INotifyCollectionChanged?)currentInstance, new(NotifyCollectionChangedAction.Reset));
        else if (onChangeProperty != null)
          onChangeProperty(strongTarget, currentInstance);
      }

      if (onChangeCollection != null && currentInstance is INotifyCollectionChanged collection)
        subs.Add(_bindCollection(strongTarget, weakTarget, collection, onChangeCollection));
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
      _source.PropertyChanged += _onChanged;
    }

    private void _onChanged(object? sender, PropertyChangedEventArgs e) {
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
      Source.CollectionChanged += _onChanged;
    }

    private void _onChanged(object? sender, NotifyCollectionChangedEventArgs e) {
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

  /* Obsolete */

  [Obsolete]
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

  [Obsolete]
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

  [Obsolete]
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
    Action<TTarget, TProp?> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    var propertyName = GetPropertyName(propertyExpression);
    var getter = GetterCache.GetGetter<TSource, TProp>(propertyName);
    var weakTarget = new WeakReference<TTarget>(target);

    var table = _propertySubs.GetOrCreateValue(source);
    var sub = table.GetOrAdd(source, propertyName, s => getter((TSource)s!));

    void handler(object? value) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, (TProp?)value);
      else
        sub.RemoveHandler(handler);
    }

    if (invokeInitOnChange)
      onChange(target, (TProp?)getter(source));

    return sub.AddHandler(handler);
  }

  [Obsolete]
  public static TTarget WithBind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression,
    Action<TTarget, TProp?> onChange)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    target.Bind(source, propertyExpression, onChange);
    return target;
  }

  [Obsolete]
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
}