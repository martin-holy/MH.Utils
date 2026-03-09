using System;

namespace MH.Utils.Disposables;

public static class DisposableExtensions {
  public static T DisposeWith<T>(this T disposable, BindingScope scope) where T : IDisposable {
    scope.Add(disposable);
    return disposable;
  }
}