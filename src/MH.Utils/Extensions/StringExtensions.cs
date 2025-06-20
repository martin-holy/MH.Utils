﻿using System;
using System.Linq;

namespace MH.Utils.Extensions;

public static class StringExtensions {
  public static int IntParseOrDefault(this string s, int d) =>
    int.TryParse(s, out var result) ? result : d;

  public static int FirstIndexOfLetter(this string s) {
    var index = 0;
    while (s.Length - 1 > index) {
      if (char.IsLetter(s, index))
        break;
      index++;
    }

    return index;
  }

  public static bool TryParseDoubleUniversal(this string s, out double result) {
    result = 0.0;
    if (string.IsNullOrEmpty(s)) return false;
    var clean = new string(s.Where(x => char.IsDigit(x) || x == '.' || x == ',' || x == '-').ToArray());
    if (clean.Length < 3) return false;
    var iOfSep = clean.LastIndexOfAny([',', '.']);
    if (iOfSep < 1) return false;
    var partA = clean[..iOfSep].Replace(",", string.Empty).Replace(".", string.Empty);
    var partB = clean[(iOfSep + 1)..];
    if (!int.TryParse(partA, out var intA)) return false;
    if (!int.TryParse(partB, out var intB)) return false;
    if (intA < 0) intB *= -1;
    var dp = double.Parse("1".PadRight(partB.Length + 1, '0'));

    result = intA + intB / dp;
    return true;
  }

  /// <summary>
  /// Replaces first string format item with count and second with 's' if count > 1.
  /// </summary>
  public static string Plural(this string s, int count) =>
    string.Format(s, count, count > 1 ? "s" : string.Empty);

  public static bool TryIndexOf(this string text, string value, ref int index,
    StringComparison comparisonType = StringComparison.OrdinalIgnoreCase) {

    var idx = text.IndexOf(value, index, comparisonType);
    if (idx != -1) index = idx;

    return idx != -1;
  }

  public static string ReplaceNewLineChars(this string text, string with) =>
    text.Replace("\r\n", with).Replace("\n", with).Replace("\r", with);

  public static double? ToDouble(this string? value, IFormatProvider provider) =>
    string.IsNullOrEmpty(value) ? null : double.Parse(value, provider);

  /// <summary>
  /// Returns a copy of this string converted to snake_case.
  /// </summary>
  /// <returns>A string in snake_case</returns>
  public static string ToSnakeCase(this string s) =>
    string.Concat(s.Select((c, i) => char.IsUpper(c) ? i == 0 ? c.ToString().ToLower() : "_" + char.ToLower(c) : c.ToString()));
}