using MH.Utils.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MH.Utils.BaseClasses;

public class AsyncRelayCommand : RelayCommandBase, IAsyncCommand {
  private NotifyTaskCompletion? _execution;

  public CancelAsyncCommand CancelCommand { get; }
  public NotifyTaskCompletion? Execution { get => _execution; protected set { _execution = value; OnPropertyChanged(); } }
  protected Func<CancellationToken, Task>? _commandFunc;

  protected AsyncRelayCommand(string? icon, string? text) : base(icon, text) {
    CancelCommand = new();
  }

  public AsyncRelayCommand(Func<CancellationToken, Task> command, string? icon = null, string? text = null) : this(icon, text) {
    _commandFunc = command;
  }

  public AsyncRelayCommand(Func<CancellationToken, Task> command, Func<bool> canExecute, string? icon = null, string? text = null) : this(icon, text) {
    _commandFunc = command;
    _canExecuteFunc = canExecute;
  }

  public override bool CanExecute(object? parameter) =>
    (_execution == null || _execution.IsCompleted) && base.CanExecute(parameter);

  public virtual async void Execute(object? parameter) {
    CancelCommand.NotifyCommandStarting();
    await ExecuteAsync(parameter, _commandFunc!(CancelCommand.Token));
  }

  public virtual async Task ExecuteAsync(object? parameter, Task task) {
    Execution = new(task, true);
    _raiseCanExecuteChanged();
    await Execution.TaskCompletion;
    CancelCommand.NotifyCommandFinished();
    _raiseCanExecuteChanged();
  }
}

public sealed class CancelAsyncCommand : RelayCommandBase, ICommand {
  private CancellationTokenSource _cts = new();
  private bool _executing;

  public CancellationToken Token => _cts.Token;

  public CancelAsyncCommand() {
    Text = "Cancel";
  }

  ~CancelAsyncCommand() {
    _cts.Dispose();
  }

  public void NotifyCommandStarting() {
    _executing = true;
    if (!_cts.IsCancellationRequested) return;
    _cts.Dispose();
    _cts = new();
    _raiseCanExecuteChanged();
  }

  public void NotifyCommandFinished() {
    _executing = false;
    _raiseCanExecuteChanged();
  }

  bool ICommand.CanExecute(object? parameter) =>
    _executing && !_cts.IsCancellationRequested;

  public void Execute(object? parameter) {
    _cts.Cancel();
    _raiseCanExecuteChanged();
  }
}

public class AsyncRelayCommand<T> : AsyncRelayCommand {
  protected Func<T?, CancellationToken, Task>? _commandParamFunc;
  protected Func<T?, bool>? _canExecuteParamFunc;

  public AsyncRelayCommand(Func<CancellationToken, Task> command, Func<T?, bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandFunc = command;
    _canExecuteParamFunc = canExecute;
  }

  public AsyncRelayCommand(Func<T?, CancellationToken, Task> command, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamFunc = command;
  }

  public AsyncRelayCommand(Func<T?, CancellationToken, Task> command, Func<bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamFunc = command;
    _canExecuteFunc = canExecute;
  }

  public AsyncRelayCommand(Func<T?, CancellationToken, Task> command, Func<T?, bool> canExecute, string? icon = null, string? text = null) : base(icon, text) {
    _commandParamFunc = command;
    _canExecuteParamFunc = canExecute;
  }

  public override bool CanExecute(object? parameter) {
    if (Execution is { IsCompleted: false }) return false;
    if (_canExecuteFunc != null) return _canExecuteFunc();
    if (_canExecuteParamFunc != null) return _canExecuteParamFunc(_cast(parameter));

    return true;
  }

  public override async void Execute(object? parameter) {
    CancelCommand.NotifyCommandStarting();
    var task = (_commandFunc != null
      ? _commandFunc(CancelCommand.Token)
      : _commandParamFunc?.Invoke(_cast(parameter), CancelCommand.Token)) ?? Task.CompletedTask;
    await ExecuteAsync(parameter, task);
  }

  private static T? _cast(object? parameter) =>
    parameter is T cast ? cast : default;
}