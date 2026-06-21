using System;

namespace MH.Utils.DB;

public static class CsvParser {
  public static int ParseInt(ReadOnlySpan<char> str) {
    if (str.IsEmpty) return 0;

    int value = 0;

    for (int i = 0; i < str.Length; i++)
      value = value * 10 + (str[i] - '0');

    return value;
  }

  public static int ParseIntOrDefault(ReadOnlySpan<char> str, int defaultValue) {
    int value = ParseInt(str);

    return value == 0 ? defaultValue : value;
  }

  public static void ParseInts<TState>(ReadOnlySpan<char> str, TState state, Action<TState, int> action) {
    if (str.IsEmpty) return;

    int value = 0;

    for (int i = 0; i <= str.Length; i++) {
      if (i == str.Length || str[i] == ',') {
        action(state, value);
        value = 0;
        continue;
      }

      value = value * 10 + (str[i] - '0');
    }
  }
}