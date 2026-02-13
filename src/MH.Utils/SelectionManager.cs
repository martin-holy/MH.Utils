using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MH.Utils;

public class SelectionManager<T> {
  private readonly List<T> _ordered = new();
  private readonly HashSet<T> _set;

  public IReadOnlyList<T> OrderedSelectedItems => _ordered;
  public IReadOnlyCollection<T> SelectedItems => _set;

  public bool IsMultiSelect { get; set; }

  public event Action<T?, T?>? SingleSelectionChanged;
  public event Action<T, bool>? ItemSelectionChanged;
  public event Action<T[]>? Cleared;

  public SelectionManager(IEqualityComparer<T>? comparer = null) {
    _set = new HashSet<T>(comparer);
  }

  public T? SelectedItem => _ordered.Count > 0 ? _ordered[0] : default;

  public bool IsSelected(T item) => _set.Contains(item);

  public void Toggle(T item) {
    if (IsMultiSelect) {
      if (_set.Contains(item))
        Deselect(item);
      else
        Select(item);
    }
    else {
      var old = SelectedItem;
      Clear();
      Select(item);
      SingleSelectionChanged?.Invoke(old, item);
    }
  }

  public void Select(T item) {
    if (_set.Add(item)) {
      _ordered.Add(item);
      ItemSelectionChanged?.Invoke(item, true);
    }
  }

  public void Deselect(T item) {
    if (_set.Remove(item)) {
      _ordered.Remove(item);
      ItemSelectionChanged?.Invoke(item, false);
    }
  }

  public void Clear() {
    if (_ordered.Count == 0) return;

    var old = _ordered.ToArray();
    _set.Clear();
    _ordered.Clear();
    Cleared?.Invoke(old);
  }
}