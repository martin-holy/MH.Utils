using System;
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

  public static string GetPropertyName<TSource, TProp>(Expression<Func<TSource, TProp>> propertyExpression) {
    if (propertyExpression.Body is not MemberExpression m)
      throw new ArgumentException("Expression must be a property access", nameof(propertyExpression));

    return m.Member.Name;
  }

  // Non-nested property binding
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

  // Nested property binding
  public static IDisposable BindNested<TTarget, TSource, TProp>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TProp>> expr,
    Action<TTarget, TProp> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    return _bindNested(target, source, expr,
      onLeafValue: (t, v) => onChange(t, (TProp)v!),
      onLeafCollection: null,
      invokeInit: invokeInitOnChange);
  }

  // Nested collection binding
  public static IDisposable Bind<TTarget, TSource, TCol>(
    this TTarget target,
    TSource source,
    Expression<Func<TSource, TCol?>> expr,
    Action<TTarget, TCol?, NotifyCollectionChangedEventArgs> onChange,
    bool invokeInitOnChange = true)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged
    where TCol : class, INotifyCollectionChanged {

    return _bindNested(target, source, expr,
      onLeafValue: null,
      onLeafCollection: (t, c, e) => onChange(t, (TCol?)c, e),
      invokeInit: invokeInitOnChange);
  }

  // Direct collection binding (no source property)
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

  private static IDisposable _bindNested<TTarget, TSource, TLeaf>(
    TTarget target,
    TSource root,
    Expression<Func<TSource, TLeaf>> expr,
    Action<TTarget, object>? onLeafValue,
    Action<TTarget, INotifyCollectionChanged, NotifyCollectionChangedEventArgs>? onLeafCollection,
    bool invokeInit)
    where TTarget : class
    where TSource : class, INotifyPropertyChanged {

    var members = _getNestedMembers(expr); // leaf last
    int hopCount = members.Count;

    var propertyNames = new string[hopCount];
    var getters = new IHopGetter[hopCount];

    for (int i = 0; i < hopCount; i++) {
      var me = members[i];
      bool isLeaf = i == hopCount - 1;

      if (!isLeaf) {
        var declaringType = me.Expression!.Type;
        if (!typeof(INotifyPropertyChanged).IsAssignableFrom(declaringType))
          throw new InvalidOperationException(
              $"Property '{me.Member.Name}' of '{declaringType}' must implement INotifyPropertyChanged.");
      }

      propertyNames[i] = me.Member.Name;
      getters[i] = HopGetterCache.GetOrAdd(me.Member);
    }

    var subscriptions = new List<IDisposable>();
    var weakTarget = new WeakReference<TTarget>(target);
    bool disposed = false;

    void RebuildFrom(int startHop) {
      if (disposed || !weakTarget.TryGetTarget(out var tTarget)) return;

      // Dispose subscriptions from this hop onward
      for (int i = subscriptions.Count - 1; i >= startHop; i--) {
        subscriptions[i].Dispose();
        subscriptions.RemoveAt(i);
      }

      object? currentInstance = root;
      for (int i = 0; i < startHop; i++) {
        if (currentInstance == null) break;
        currentInstance = getters[i].Get(currentInstance);
      }

      for (int hop = startHop; hop < hopCount; hop++) {
        if (currentInstance == null) break;

        var propertyName = propertyNames[hop];
        var capturedHop = hop;

        if (currentInstance is INotifyPropertyChanged npc) {
          var declaringType = members[capturedHop].Expression!.Type;
          var propType = members[capturedHop].Type;

          var sub = AddPropertyTableHandler(
              npc,
              currentInstance!,
              declaringType,
              propType,
              propertyName,
              () => {
                if (weakTarget.TryGetTarget(out _))
                  RebuildFrom(capturedHop + 1);
              }
          );
          subscriptions.Add(sub);
        }

        currentInstance = getters[hop].Get(currentInstance);
      }

      if (!weakTarget.TryGetTarget(out tTarget)) return;

      if (onLeafCollection != null && currentInstance is INotifyCollectionChanged collection) {
        var table = _collectionSubs.GetOrCreateValue(collection);
        var collSub = table.GetOrAdd(collection);

        void CollectionHandler(object? s, NotifyCollectionChangedEventArgs e) {
          if (weakTarget.TryGetTarget(out var t))
            onLeafCollection(t, collection, e);
          else
            collSub.RemoveHandler(CollectionHandler);
        }

        subscriptions.Add(collSub.AddHandler(CollectionHandler));

        if (invokeInit)
          onLeafCollection(tTarget, collection, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
      else if (onLeafValue != null) {
        if (invokeInit)
          onLeafValue(tTarget, currentInstance!);
      }
    }

    RebuildFrom(0);

    return new NestedDispose(subscriptions);
  }

  // Extract nested member expressions from lambda
  private static List<MemberExpression> _getNestedMembers<TSource, TProp>(Expression<Func<TSource, TProp>> pathExpr) {
    var members = new List<MemberExpression>();
    Expression? e = pathExpr.Body;
    while (e is MemberExpression me) {
      members.Insert(0, me);
      e = me.Expression;
    }
    return members;
  }

  private sealed class Handler : IDisposable {
    public string PropertyName = default!;
    public Action OnChanged = default!;
    public INotifyPropertyChanged TargetNode = default!;

    public void OnEvent(object? sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == PropertyName)
        OnChanged();
    }

    public void Dispose() {
      TargetNode.PropertyChanged -= OnEvent;

      // Reset fields before returning to pool
      PropertyName = default!;
      OnChanged = default!;
      TargetNode = default!;

      HandlerPool.Add(this);
    }
  }

  private static readonly ConcurrentBag<Handler> HandlerPool = new();

  public static IDisposable AddPropertyTableHandler(
    INotifyPropertyChanged nodeInstance,
    object sourceInstance,
    Type declaringType,
    Type propType,
    string propertyName,
    Action onChanged) {

    var h = HandlerPool.TryTake(out var handler) ? handler : new Handler();
    h.PropertyName = propertyName;
    h.OnChanged = onChanged;
    h.TargetNode = nodeInstance;

    nodeInstance.PropertyChanged += h.OnEvent;

    return h;
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

  internal interface IHopGetter {
    object? Get(object? parent);
  }

  internal sealed class HopGetter<TParent, TChild> : IHopGetter where TParent : class {
    private readonly Func<TParent, TChild> _getter;

    public HopGetter(Func<TParent, TChild> getter) {
      _getter = getter;
    }

    public object? Get(object? parent) => _getter((TParent)parent!);

    public static HopGetter<TParent, TChild> Create(PropertyInfo property) {
      var param = Expression.Parameter(typeof(TParent), "p");
      var access = Expression.Property(param, property);
      var lambda = Expression.Lambda<Func<TParent, TChild>>(access, param);
      var compiled = lambda.Compile();

      return new HopGetter<TParent, TChild>(compiled);
    }
  }

  internal static class HopGetterCache {
    private static readonly ConcurrentDictionary<PropertyInfo, IHopGetter> _cache = new();

    public static IHopGetter GetOrAdd(MemberInfo member) {
      if (member is not PropertyInfo prop)
        throw new ArgumentException("Only property members are supported.", nameof(member));

      return _cache.GetOrAdd(prop, static p => {
        var parentType = p.DeclaringType!;
        var childType = p.PropertyType;
        var genericType = typeof(HopGetter<,>).MakeGenericType(parentType, childType);
        var createMethod = genericType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;

        return (IHopGetter)createMethod.Invoke(null, new object[] { p })!;
      });
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