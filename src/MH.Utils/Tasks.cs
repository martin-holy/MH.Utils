using System;
using System.Threading.Tasks;

namespace MH.Utils;

public static class Tasks {
  public static TaskScheduler UiTaskScheduler { get; private set; } = null!;

  public static void SetUiTaskScheduler() =>
    UiTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

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
    Task.Run(work).ContinueWith(task => {
      if (task.IsFaulted)
        onError(task.Exception?.InnerException);
      else
        onSuccess(task.Result);
    }, UiTaskScheduler);
  }
}