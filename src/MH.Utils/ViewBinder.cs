using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MH.Utils;

public sealed class ViewBinder<TView, TValue> : IDisposable where TView : class {
  private readonly WeakReference<TView> _viewRef;
  private readonly Action<EventHandler<TValue>>? _subscribe;
  private readonly Action<EventHandler<TValue>>? _unsubscribe;
  private readonly Action<TView, TValue> _setViewValue;
  private readonly EventHandler<TValue>? _viewChangedHandler;

  private IDisposable? _vmSubscription;
  private Action<TValue>? _vmSetter;
  private bool _updating;
  private bool _disposed;
  private readonly bool _isTwoWay;

  public ViewBinder(
    TView view,
    Action<EventHandler<TValue>> subscribe,
    Action<EventHandler<TValue>> unsubscribe,
    Action<TView, TValue> setViewValue) {

    _viewRef = new WeakReference<TView>(view);
    _subscribe = subscribe;
    _unsubscribe = unsubscribe;
    _setViewValue = setViewValue;
    _viewChangedHandler = _onViewChanged;
    _isTwoWay = true;

    _subscribe(_viewChangedHandler);
  }

  public ViewBinder(TView view, Action<TView, TValue> setViewValue) {
    _viewRef = new WeakReference<TView>(view);
    _setViewValue = setViewValue;
    _isTwoWay = false;
  }

  private void _onViewChanged(object? sender, TValue newValue) {
    if (_updating) return;

    if (!_viewRef.TryGetTarget(out var view)) {
      _unsubscribe?.Invoke(_viewChangedHandler!);
      _vmSubscription?.Dispose();
      return;
    }

    _vmSetter?.Invoke(newValue);
  }

  public void Bind<TSource, TProp>(TSource source, Expression<Func<TSource, TProp>> propertyExpression)
    where TSource : class, INotifyPropertyChanged {

    _vmSubscription?.Dispose();
    _vmSetter = null;

    if (!_viewRef.TryGetTarget(out var view)) return;

    // VM → View
    _vmSubscription = BindingU.Bind(view, source, propertyExpression, (_, p) => {
      if (!_viewRef.TryGetTarget(out var v)) return;

      _updating = true;
      try {
        _setViewValue(v, (TValue)Convert.ChangeType(p, typeof(TValue))!);
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

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;

    _vmSubscription?.Dispose();

    if (_unsubscribe != null && _viewChangedHandler != null)
      _unsubscribe(_viewChangedHandler);
  }
}