using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MH.Utils.Localization;

internal sealed class LanguageCatalog {
  public bool Exists { get; private init; }

  public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

  public static LanguageCatalog Load(Stream stream) {
    var result = new LanguageCatalog { Exists = true };

    using var reader = new StreamReader(
      stream,
      Encoding.UTF8,
      detectEncodingFromByteOrderMarks: true,
      leaveOpen: true);

    string? line;
    int lineNumber = 0;

    while ((line = reader.ReadLine()) != null) {
      lineNumber++;

      string trimmed = line.TrimStart();

      if (trimmed.Length == 0 || trimmed[0] == '#')
        continue;

      int separator = line.IndexOf('=');

      if (separator < 0)
        throw _invalidEntry(lineNumber, "Missing '='.");

      string key = line[..separator].Trim();

      if (key.Length == 0)
        throw _invalidEntry(lineNumber, "Empty key.");

      string value = _unescape(line[(separator + 1)..]);

      if (!result.Values.TryAdd(key, value))
        throw _invalidEntry(lineNumber, $"Duplicate key '{key}'.");
    }

    return result;

    static FormatException _invalidEntry(int number, string message) =>
      new($"Invalid language file at line {number}: {message}");
  }

  public static string Escape(string value) {
    if (!value.Contains('\\') &&
        !value.Contains('\n') &&
        !value.Contains('\r') &&
        !value.Contains('\t'))
      return value;

    var result = new StringBuilder(value.Length);

    foreach (char c in value) {
      switch (c) {
        case '\\': result.Append("\\\\"); break;
        case '\n': result.Append("\\n"); break;
        case '\r': result.Append("\\r"); break;
        case '\t': result.Append("\\t"); break;
        default: result.Append(c); break;
      }
    }

    return result.ToString();
  }

  private static string _unescape(string value) {
    if (!value.Contains('\\')) return value;

    var result = new StringBuilder(value.Length);

    for (int i = 0; i < value.Length; i++) {
      char c = value[i];

      if (c != '\\' || i + 1 >= value.Length) {
        result.Append(c);
        continue;
      }

      switch (value[++i]) {
        case 'n': result.Append('\n'); break;
        case 'r': result.Append('\r'); break;
        case 't': result.Append('\t'); break;
        case '\\': result.Append('\\'); break;
        default:
          result.Append('\\');
          result.Append(value[i]);
          break;
      }
    }

    return result.ToString();
  }
}