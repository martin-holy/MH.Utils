using System;

namespace MH.Utils.DB;

public static class IdParser {
  public static int Parse(ReadOnlySpan<char> stringId) {
    if (stringId.IsEmpty) return 0;

    int id = 0;

    for (int i = 0; i < stringId.Length; i++)
      id = id * 10 + (stringId[i] - '0');

    return id;
  }

  public static int Parse(ReadOnlySpan<char> stringId, int defaultValue) {
    int id = Parse(stringId);

    return id == 0 ? defaultValue : id;
  }

  public static void Parse<TState>(ReadOnlySpan<char> ids, TState state, Action<TState, int> action) {
    if (ids.IsEmpty) return;

    int id = 0;

    for (int i = 0; i <= ids.Length; i++) {
      if (i == ids.Length || ids[i] == ',') {
        action(state, id);
        id = 0;
        continue;
      }

      id = id * 10 + (ids[i] - '0');
    }
  }
}