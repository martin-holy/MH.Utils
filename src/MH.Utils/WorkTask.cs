using System;
using System.Threading;
using System.Threading.Tasks;

namespace MH.Utils;

public sealed class WorkTask : IDisposable {
  private CancellationTokenSource? _cts;
  private Task? _task;
  private bool _waitingForCancel;

  public CancellationToken Token { get; private set; }

  public async Task<bool> Cancel() {
    if (_task == null || _waitingForCancel) return false;

    _waitingForCancel = true;

    try {
      _cts?.Cancel();
      await _task;
    }
    catch (OperationCanceledException) {
      // expected
    }
    catch (Exception ex) {
      Log.Error(ex);
    }
    finally {
      _waitingForCancel = false;
    }

    return true;
  }

  public async Task Start(Func<CancellationToken, Task> work) {
    _cts = new();
    Token = _cts.Token;

    try {
      _task = work(Token);
      await _task;
    }
    finally {
      Dispose();
    }
  }

  public void Dispose() {
    try {
      _task?.Dispose();
      _cts?.Dispose();
      _cts = null;
    }
    catch {
      // ignored
    }
  }
}