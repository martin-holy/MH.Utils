using MH.Utils.BaseClasses;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public static class BindingU {
  public enum Mode { OneWay, TwoWay }

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
      return new Subscription(() => RemoveHandler(handler));
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
      return new Subscription(() => RemoveHandler(handler));
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

  private class Subscription(Action _unsubscribe) : IDisposable {
    public void Dispose() => _unsubscribe();
  }
}