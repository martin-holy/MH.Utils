using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace MH.Utils.Binding;

public sealed class ViewBinder<TView, TValue> where TView : class {
  private readonly WeakReference<TView> _viewRef;
  private readonly Action<EventHandler<TValue>> _subscribe;
  private readonly Action<EventHandler<TValue>> _unsubscribe;
  private readonly Func<TView, TValue> _getViewValue;
  private readonly Action<TView, TValue> _setViewValue;
  private readonly EventHandler<TValue> _viewChangedHandler;

  private IDisposable? _vmSubscription;
  private Action<TValue>? _vmSetter;
  private bool _updating;

  public ViewBinder(
    TView view,
    Action<EventHandler<TValue>> subscribe,
    Action<EventHandler<TValue>> unsubscribe,
    Func<TView, TValue> getViewValue,
    Action<TView, TValue> setViewValue) {

    _viewRef = new WeakReference<TView>(view);
    _subscribe = subscribe;
    _unsubscribe = unsubscribe;
    _getViewValue = getViewValue;
    _setViewValue = setViewValue;

    _viewChangedHandler = OnViewChanged;
    _subscribe(_viewChangedHandler);
  }

  private void OnViewChanged(object? sender, TValue newValue) {
    if (_updating) return;

    if (!_viewRef.TryGetTarget(out var view)) {
      _unsubscribe?.Invoke(_viewChangedHandler);
      _vmSubscription?.Dispose();
      return;
    }

    _vmSetter?.Invoke(newValue);
  }

  public void Bind<TSource, TProp>(
    TSource source,
    Expression<Func<TSource, TProp>> propertyExpression,
    BindingU.Mode mode = BindingU.Mode.TwoWay)
    where TSource : class, INotifyPropertyChanged {

    _vmSubscription?.Dispose();
    _vmSetter = null;

    if (propertyExpression.Body is not MemberExpression m)
      throw new ArgumentException("Expression must be a property access", nameof(propertyExpression));

    var propName = m.Member.Name;
    var getter = BindingU.GetterCache.GetGetter<TSource, TProp>(propName);
    var setter = BindingU.SetterCache.GetSetter<TSource, TProp>(propName);

    if (!_viewRef.TryGetTarget(out var view)) return;

    // VM → View
    _vmSubscription = BindingU.Bind(view, source, propertyExpression, (_, p) => {
      if (!_viewRef.TryGetTarget(out var v)) return;

      _updating = true;
      try {
        var converted = (TValue)Convert.ChangeType(p, typeof(TValue))!;
        _setViewValue(v, converted);
      }
      finally { _updating = false; }
    });

    // View → VM
    if (mode == BindingU.Mode.TwoWay) {
      _vmSetter = val => {
        if (_updating) return;
        var converted = Convert.ChangeType(val, typeof(TProp))!;
        setter(source, (TProp)converted);
      };
    }
  }
}