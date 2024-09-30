﻿using System;
using System.Collections.Generic;
using System.Net;

namespace MH.Utils;

public class StringRange {
  public string StartString { get; init; }
  public string? StartEndString { get; init; }
  public string? EndString { get; init; }
  public int Start { get; private set; }
  public int End { get; set; }
  public StringComparison ComparisonType { get; init; } = StringComparison.OrdinalIgnoreCase;

  public StringRange(string startString) {
    StartString = startString;
  }

  public StringRange(string startString, string endString) {
    StartString = startString;
    EndString = endString;
  }

  public StringRange(string startString, string startEndString, string endString) {
    StartString = startString;
    StartEndString = startEndString;
    EndString = endString;
  }

  public IEnumerable<StringRange?> AsEnumerable(string text, StringRange innerRange) {
    var idx = Start;
    return AsEnumerable(() => innerRange.From(text, ref idx, End));
  }

  public IEnumerable<T> AsEnumerable<T>(Func<T?> func) {
    while (true) {
      if (func() is not { } value) yield break;
      yield return value;
    }
  }

  /// <summary>
  /// Don't forget to set StringRange.End in convertor to be able to move to next section if StringRange doesn't have EndString!
  /// </summary>
  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, Func<string, StringRange, T?> convertor) =>
    AsEnumerable(text, startIdx, -1, convertor);

  /// <summary>
  /// Don't forget to set StringRange.End in convertor to be able to move to next section if StringRange doesn't have EndString!
  /// </summary>
  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, int endIdx, Func<string, StringRange, T?> convertor) {
    while (true) {
      if (From(text, startIdx, endIdx) is not { } range
          || convertor(text, range) is not { } item)
        yield break;
      
      startIdx = End;
      yield return item;
    }
  }

  public int AsInt32(string text, int ifNull = 0) =>
    int.TryParse(AsString(text), out var i) ? i : ifNull;

  public string AsString(string text) =>
    text[Start..End];

  public string AsHtmlDecodedString(string text) =>
    WebUtility.HtmlDecode(AsString(text));

  public StringRange? From(string text, ref int searchStart, int searchEnd = -1) {
    if (!Found(text, searchStart, searchEnd)) return null;
    searchStart = Start;
    return this;
  }

  public StringRange? From(string text, int searchStart, int searchEnd = -1) =>
    Found(text, searchStart, searchEnd) ? this : null;

  public StringRange? From(string text, StringRange range) =>
    Found(text, range.Start, range.End + 1) ? this : null;

  public bool Found(string text, int searchStart, int searchEnd = -1) {
    var count = GetCountForIndexOf(text, searchStart, searchEnd);

    // search start
    Start = text.IndexOf(StartString, searchStart, count, ComparisonType);
    if (Start == -1) return false;
    Start += StartString.Length;

    // optionally search for start end
    if (!string.IsNullOrEmpty(StartEndString)) {
      count = GetCountForIndexOf(text, Start, searchEnd);
      Start = text.IndexOf(StartEndString, Start, count, ComparisonType);
      if (Start == -1) return false;
      Start += StartEndString.Length;
    }

    // search for end
    if (string.IsNullOrEmpty(EndString)) {
      End = searchEnd == -1 ? text.Length - 1 : searchEnd;
    }
    else {
      count = GetCountForIndexOf(text, Start, searchEnd);
      End = text.IndexOf(EndString, Start, count, ComparisonType);
      if (End == -1) return false;
    }

    return true;
  }

  private static int GetCountForIndexOf(string text, int searchStart, int searchEnd = -1) =>
    searchEnd == -1 ? text.Length - searchStart : searchEnd - searchStart;
}