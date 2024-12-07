using System;
using System.Windows.Input;

namespace MH.Utils.BaseClasses;

public class RelayCommand : RelayCommandBase, ICommand {
  protected Action? _commandAction;

  protected RelayCommand() { }

  protected RelayCommand(string? icon, string? text) : base(icon, text) { }

  public RelayCommand(Action command, string? icon = null, string? text = null) : base(icon, text) {
    _commandAction = command;
  }

  public RelayCommand(Action command, Func<bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandAction = command;
    _canExecuteFunc = canExecute;
  }

  public virtual void Execute(object? parameter) =>
    _commandAction?.Invoke();
}

public class RelayCommand<T> : RelayCommand {
  protected Action<T?>? _commandParamAction;
  protected Func<T?, bool>? _canExecuteParamFunc;

  public RelayCommand(Action command, Func<T?, bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandAction = command;
    _canExecuteParamFunc = canExecute;
  }

  public RelayCommand(Action<T?> command, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamAction = command;
  }

  public RelayCommand(Action<T?> command, Func<bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamAction = command;
    _canExecuteFunc = canExecute;
  }

  public RelayCommand(Action<T?> command, Func<T?, bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamAction = command;
    _canExecuteParamFunc = canExecute;
  }

  public override bool CanExecute(object? parameter) {
    if (_canExecuteFunc != null) return _canExecuteFunc();
    if (_canExecuteParamFunc != null) return _canExecuteParamFunc(_cast(parameter));

    return true;
  }

  public override void Execute(object? parameter) {
    _commandAction?.Invoke();
    _commandParamAction?.Invoke(_cast(parameter));
  }

  private static T? _cast(object? parameter) =>
    parameter is T cast ? cast : default;
}