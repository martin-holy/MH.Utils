﻿using System;
using System.Collections.Generic;
using System.Net;

namespace MH.Utils;

public class StringRange(string startString) {
  public delegate T ClosedConvertorFunc<out T>(string text, StringRange range);
  public delegate T OpenConvertorFunc<out T>(string text, StringRange range, out int rangeEnd);

  public string StartString { get; init; } = startString;
  public string? StartEndString { get; init; }
  public string? EndString { get; init; }
  public int Start { get; private set; }
  public int End { get; set; }
  public StringComparison ComparisonType { get; init; } = StringComparison.OrdinalIgnoreCase;

  public StringRange(string startString, string endString) :
    this(startString) => EndString = endString;

  public StringRange(string startString, string startEndString, string endString) :
    this(startString, endString) => StartEndString = startEndString;

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

  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, ClosedConvertorFunc<T?> convertor) =>
    AsEnumerable(text, startIdx, -1, convertor);

  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, int endIdx, ClosedConvertorFunc<T?> convertor) {
    while (true) {
      if (From(text, startIdx, endIdx) is not { } range
          || convertor(text, range) is not { } item)
        yield break;

      startIdx = End;
      yield return item;
    }
  }

  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, OpenConvertorFunc<T?> convertor) =>
    AsEnumerable(text, startIdx, -1, convertor);

  public IEnumerable<T> AsEnumerable<T>(string text, int startIdx, int endIdx, OpenConvertorFunc<T?> convertor) {
    while (true) {
      if (From(text, startIdx, endIdx) is not { } range
          || convertor(text, range, out var rangeEnd) is not { } item)
        yield break;
      
      startIdx = rangeEnd;
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
    var count = _getCountForIndexOf(text, searchStart, searchEnd);

    // search start
    Start = text.IndexOf(StartString, searchStart, count, ComparisonType);
    if (Start == -1) return false;
    Start += StartString.Length;

    // optionally search for start end
    if (!string.IsNullOrEmpty(StartEndString)) {
      count = _getCountForIndexOf(text, Start, searchEnd);
      Start = text.IndexOf(StartEndString, Start, count, ComparisonType);
      if (Start == -1) return false;
      Start += StartEndString.Length;
    }

    // search for end
    if (string.IsNullOrEmpty(EndString)) {
      End = searchEnd == -1 ? text.Length - 1 : searchEnd;
    }
    else {
      count = _getCountForIndexOf(text, Start, searchEnd);
      End = text.IndexOf(EndString, Start, count, ComparisonType);
      if (End == -1) return false;
    }

    return true;
  }

  private static int _getCountForIndexOf(string text, int searchStart, int searchEnd = -1) =>
    searchEnd == -1 ? text.Length - searchStart : searchEnd - searchStart;
}