using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public sealed class ViewBinder<TTarget, TSource, TProp, TValue> : IDisposable
  where TTarget : class
  where TSource : class, INotifyPropertyChanged {

  private readonly WeakReference<TTarget> _weakTarget;
  private readonly Action<EventHandler<TValue>>? _subscribe;
  private readonly Action<EventHandler<TValue>>? _unsubscribe;
  private readonly Action<TTarget, TValue> _setValue;
  private readonly EventHandler<TValue>? _viewChangedHandler;

  private IDisposable? _vmSubscription;
  private Action<TValue>? _vmSetter;
  private bool _updating;
  private bool _disposed;

  public ViewBinder(
    TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TProp> getter,
    Action<TSource, TProp> setter,
    Action<TTarget, TValue> setValue,
    Action<EventHandler<TValue>> subscribe,
    Action<EventHandler<TValue>> unsubscribe) {

    _weakTarget = new WeakReference<TTarget>(target);
    _subscribe = subscribe;
    _unsubscribe = unsubscribe;
    _setValue = setValue;
    _viewChangedHandler = _onViewChanged;
    _subscribe(_viewChangedHandler);
    _bind(source, propertyName, getter, setter);
  }

  public ViewBinder(
    TTarget target,
    TSource source,
    string propertyName,
    Func<TSource, TProp> getter,
    Action<TTarget, TValue> setValue) {

    _weakTarget = new WeakReference<TTarget>(target);
    _setValue = setValue;
    _bind(source, propertyName, getter, null);
  }

  private void _onViewChanged(object? sender, TValue newValue) {
    if (_updating) return;

    if (!_weakTarget.TryGetTarget(out var _)) {
      _unsubscribe?.Invoke(_viewChangedHandler!);
      _vmSubscription?.Dispose();
      return;
    }

    _vmSetter?.Invoke(newValue);
  }

  private void _bind(TSource source, string propertyName, Func<TSource, TProp> getter, Action<TSource, TProp>? setter) {
    _vmSubscription?.Dispose();
    _vmSetter = null;

    if (!_weakTarget.TryGetTarget(out var target)) return;

    // VM → View
    _vmSubscription = BindingU.Bind(target, source, propertyName, getter, (t, v) => {
      _updating = true;
      try {
        _setValue(t, (TValue)Convert.ChangeType(v, typeof(TValue))!);
      }
      finally { _updating = false; }
    });

    // View → VM
    if (setter != null) {
      _vmSetter = v => {
        if (!_updating)
          setter(source, (TProp)Convert.ChangeType(v, typeof(TProp))!);
      };
    }
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;

    _vmSubscription?.Dispose();

    if (_unsubscribe != null && _viewChangedHandler != null)
      _unsubscribe(_viewChangedHandler);
  }

  /* Obsolete */

  [Obsolete]
  private bool _isTwoWay;

  [Obsolete]
  public ViewBinder(
    TTarget target,
    Action<EventHandler<TValue>> subscribe,
    Action<EventHandler<TValue>> unsubscribe,
    Action<TTarget, TValue> setValue,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression) {

    _weakTarget = new WeakReference<TTarget>(target);
    _subscribe = subscribe;
    _unsubscribe = unsubscribe;
    _setValue = setValue;
    _viewChangedHandler = _onViewChanged;
    _isTwoWay = true;

    _subscribe(_viewChangedHandler);
    _bind(source, propertyExpression);
  }

  [Obsolete]
  public ViewBinder(
    TTarget target,
    Action<TTarget, TValue> setValue,
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression) {

    _weakTarget = new WeakReference<TTarget>(target);
    _setValue = setValue;
    _isTwoWay = false;

    _bind(source, propertyExpression);
  }

  [Obsolete]
  private void _bind(TSource source, Expression<Func<TSource, TProp>> propertyExpression) {
    _vmSubscription?.Dispose();
    _vmSetter = null;

    if (!_weakTarget.TryGetTarget(out var target)) return;

    // VM → View
    _vmSubscription = BindingU.Bind(target, source, propertyExpression, (_, p) => {
      if (!_weakTarget.TryGetTarget(out var t)) return;

      _updating = true;
      try {
        _setValue(t, (TValue)Convert.ChangeType(p, typeof(TValue))!);
      }
      finally { _updating = false; }
    });

    // View → VM
    if (_isTwoWay) {
      var propertyName = BindingU.GetPropertyName(propertyExpression);
      var setter = BindingU.SetterCache.GetSetter<TSource, TProp>(propertyName);

      _vmSetter = val => {
        if (!_updating)
          setter(source, (TProp)Convert.ChangeType(val, typeof(TProp))!);
      };
    }
  }
}