using MH.Utils.BaseClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public static class BindingU {
  private static readonly ConditionalWeakTable<INotifyPropertyChanged, PropertySubscriptionTable> _propertySubs = new();
  private static readonly ConditionalWeakTable<INotifyCollectionChanged, CollectionSubscriptionTable> _collectionSubs = new();

  public static IDisposable Bind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression,
    Action<TTarget, TProp> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    if (propertyExpression.Body is not MemberExpression m)
      throw new ArgumentException("Expression must be a property access", nameof(propertyExpression));

    var propertyName = m.Member.Name;
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

  public static TTarget WithBind<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression,
    Action<TTarget, TProp> onChange)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    target.Bind(source, propertyExpression, onChange);
    return target;
  }

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

  private class PropertySubscription<TSource, TProp> where TSource : class, INotifyPropertyChanged {
    private readonly TSource _source;
    private readonly string _propertyName;
    private readonly Func<TSource, TProp> _getter;
    private readonly PropertySubscriptionTable _table;
    private readonly List<Action<TProp>> _handlers = [];

    public PropertySubscription(TSource source, string propertyName, Func<TSource, TProp> getter, PropertySubscriptionTable table) {
      _source = source;
      _propertyName = propertyName;
      _getter = getter;
      _table = table;
      _source.PropertyChanged += _onChanged;
    }

    private void _onChanged(object? sender, PropertyChangedEventArgs e) {
      if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != _propertyName) return;

      var value = _getter(_source);
      foreach (var handler in _handlers.ToArray())
        handler(value);
    }

    public IDisposable AddHandler(Action<TProp> handler) {
      _handlers.Add(handler);
      return new Subscription(() => RemoveHandler(handler));
    }

    public void RemoveHandler(Action<TProp> handler) {
      _handlers.Remove(handler);
      if (_handlers.Count == 0) {
        _source.PropertyChanged -= _onChanged;
        _table.Remove(_propertyName);
      }
    }
  }

  private class CollectionSubscription {
    private readonly INotifyCollectionChanged _source;
    private readonly CollectionSubscriptionTable _table;
    private readonly List<NotifyCollectionChangedEventHandler> _handlers = [];

    public CollectionSubscription(INotifyCollectionChanged source, CollectionSubscriptionTable table) {
      _source = source;
      _table = table;
      _source.CollectionChanged += _onChanged;
    }

    private void _onChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      foreach (var handler in _handlers.ToArray())
        handler(sender, e);
    }

    public IDisposable AddHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Add(handler);
      return new Subscription(() => RemoveHandler(handler));
    }

    public void RemoveHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Remove(handler);
      if (_handlers.Count == 0) {
        _source.CollectionChanged -= _onChanged;
        _table.Clear();
      }
    }
  }

  private class PropertySubscriptionTable {
    private readonly Dictionary<string, object> _subs = new();

    public PropertySubscription<TSource, TProp> GetOrAdd<TSource, TProp>(
      TSource source, string propertyName, Func<TSource, TProp> getter)
      where TSource : class, INotifyPropertyChanged {

      if (_subs.TryGetValue(propertyName, out var existing))
        return (PropertySubscription<TSource, TProp>)existing;

      var sub = new PropertySubscription<TSource, TProp>(source, propertyName, getter, this);
      _subs[propertyName] = sub;
      return sub;
    }

    public void Remove(string propertyName) => _subs.Remove(propertyName);
  }

  private class CollectionSubscriptionTable {
    private CollectionSubscription? _sub;

    public CollectionSubscription GetOrAdd(INotifyCollectionChanged source) =>
      _sub ??= new CollectionSubscription(source, this);

    public void Clear() => _sub = null;
  }

  private static class GetterCache {
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

  private class Subscription(Action _unsubscribe) : IDisposable {
    public void Dispose() => _unsubscribe();
  }
}