using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.Extensions; 

public static class EnumerableExtensions {
  public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? items) =>
    items ?? [];

  public static IEnumerable<int>? ToHashCodes<T>(this IEnumerable<T>? items) =>
    items?.Select(x => x!.GetHashCode());

  public static string ToCsv<T>(this IEnumerable<T>? items, string separator = ",") =>
    items == null
      ? string.Empty
      : string.Join(separator, items.Select(x => x!.ToString()));

  /// <summary>
  /// Generates an <see cref="IEnumerable{T}"/> by repeatedly calling the provided function until it returns <c>null</c>.
  /// </summary>
  public static IEnumerable<T> AsEnumerable<T>(Func<T?> func) {
    while (true) {
      if (func() is not { } value) yield break;
      yield return value;
    }
  }
}