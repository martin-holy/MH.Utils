using MH.Utils.BaseClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MH.Utils;

public static class BindingU {
  private static readonly Dictionary<(INotifyPropertyChanged, string), PropertySubscription> _propertySubs = [];
  private static readonly Dictionary<INotifyCollectionChanged, CollectionSubscription> _collectionSubs = [];

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
    var getter = GetterCache.GetGetter(typeof(TSource), propertyName);
    var weakTarget = new WeakReference<TTarget>(target);

    if (!_propertySubs.TryGetValue((source, propertyName), out var sub)) {
      sub = new PropertySubscription(source, propertyName, getter);
      _propertySubs[(source, propertyName)] = sub;
    }

    void handler(object? valueObj) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, (TProp)valueObj!);
      else
        sub.RemoveHandler(handler);
    }

    if (invokeInitOnChange)
      onChange(target, (TProp)getter(source)!);

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

    if (!_collectionSubs.TryGetValue(source, out var sub)) {
      sub = new CollectionSubscription(source);
      _collectionSubs[source] = sub;
    }

    void handler(object? s, NotifyCollectionChangedEventArgs e) {
      if (weakTarget.TryGetTarget(out var t))
        onChange(t, e);
      else
        sub.RemoveHandler(handler);
    }

    onChange(target, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    return sub.AddHandler(handler);
  }

  private class PropertySubscription {
    private readonly INotifyPropertyChanged _source;
    private readonly string _propertyName;
    private readonly Func<object, object?> _getter;
    private readonly List<Action<object?>> _handlers = [];

    public PropertySubscription(INotifyPropertyChanged source, string propertyName, Func<object, object?> getter) {
      _source = source;
      _propertyName = propertyName;
      _getter = getter;
      _source.PropertyChanged += _onChanged;
    }

    private void _onChanged(object? sender, PropertyChangedEventArgs e) {
      if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != _propertyName) return;

      var value = _getter(_source);
      foreach (var handler in _handlers.ToArray())
        handler(value);
    }

    public Subscription AddHandler(Action<object?> handler) {
      _handlers.Add(handler);
      return new(() => RemoveHandler(handler));
    }

    public void RemoveHandler(Action<object?> handler) {
      _handlers.Remove(handler);
      if (_handlers.Count == 0) {
        _source.PropertyChanged -= _onChanged;
        _propertySubs.Remove((_source, _propertyName));
      }
    }
  }

  private class CollectionSubscription {
    private readonly INotifyCollectionChanged _source;
    private readonly List<NotifyCollectionChangedEventHandler> _handlers = [];

    public CollectionSubscription(INotifyCollectionChanged source) {
      _source = source;
      _source.CollectionChanged += _onChanged;
    }

    private void _onChanged(object? sender, NotifyCollectionChangedEventArgs e) {
      foreach (var handler in _handlers.ToArray())
        handler(sender, e);
    }

    public Subscription AddHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Add(handler);
      return new(() => RemoveHandler(handler));
    }

    public void RemoveHandler(NotifyCollectionChangedEventHandler handler) {
      _handlers.Remove(handler);
      if (_handlers.Count == 0) {
        _source.CollectionChanged -= _onChanged;
        _collectionSubs.Remove(_source);
      }
    }
  }

  private static class GetterCache {
    private static readonly Dictionary<(Type, string), Func<object, object?>> _cache = [];

    public static Func<object, object?> GetGetter(Type sourceType, string propertyName) {
      var key = (sourceType, propertyName);
      if (_cache.TryGetValue(key, out var existing)) return existing;

      // build expression: (object src) => (object) ((TSource)src).Property
      var srcParam = Expression.Parameter(typeof(object), "src");
      var converted = Expression.Convert(srcParam, sourceType);
      var prop = Expression.Property(converted, propertyName);
      var boxed = Expression.Convert(prop, typeof(object));
      var lambda = Expression.Lambda<Func<object, object?>>(boxed, srcParam);
      var compiled = lambda.Compile();
      _cache[key] = compiled;
      return compiled;
    }
  }

  private class Subscription(Action _unsubscribe) : IDisposable {
    public void Dispose() => _unsubscribe();
  }
}