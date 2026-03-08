using System;
using System.Collections.Generic;

namespace MH.Utils.Disposables;

public sealed class BindingScope : IDisposable {
  private readonly List<IDisposable> _items = new();

  public void Add(IDisposable disposable) {
    _items.Add(disposable);
  }

  public void Dispose() {
    for (int i = 0; i < _items.Count; i++)
      _items[i].Dispose();

    _items.Clear();
  }
}