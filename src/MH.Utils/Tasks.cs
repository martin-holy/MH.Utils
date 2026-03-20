using System;
using System.Threading.Tasks;

namespace MH.Utils;

public static class Tasks {
  public static Task RunOnUiThread(Action action) =>
    RunOnUiThread(() => {
      action();
      return Task.CompletedTask;
    });

  public static Task RunOnUiThread(Func<Task> func) {
    var tcs = new TaskCompletionSource();

    Dispatch(async () => {
      try {
        await func();
        tcs.SetResult();
  }
      catch (Exception ex) {
        tcs.SetException(ex);
      }
    });

    return tcs.Task;
  }

  public static Action<Action> Dispatch { get; set; } = null!;

  /// <summary>
  /// Executes the work on background thread and then executes the onSuccess or the onError on UI thread
  /// </summary>
  public static void DoWork<T>(Func<T> work, Action<T> onSuccess, Action<Exception?> onError) {
    _ = DoWorkAsync(work, onSuccess, onError);
  }

  public static async Task DoWorkAsync<T>(Func<T> work, Action<T> onSuccess, Action<Exception?> onError) {
    try {
      var result = await Task.Run(work);
      await Tasks.RunOnUiThread(() => onSuccess(result));
    }
    catch (Exception ex) {
      await Tasks.RunOnUiThread(() => onError(ex));
    }
  }
}