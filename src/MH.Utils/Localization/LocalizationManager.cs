using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MH.Utils.Localization;

public static class LocalizationManager {
  private static readonly Dictionary<Assembly, Dictionary<string, string>> _assemblies = [];
  private static readonly Dictionary<(Assembly Assembly, string Language, bool User), LanguageCatalog> _catalogs = [];
  private static readonly HashSet<(Assembly Assembly, string Key)> _reportedMissing = [];
  private static readonly string[] _pluralCategories = ["zero", "one", "two", "few", "many", "other"];

  private static string[] _languages = [];
  private static string _defaultLanguage = "en";
  private static CultureInfo _culture = CultureInfo.GetCultureInfo("en");
  private static ILanguageFileProvider? _fileProvider;

  public static string DefaultLanguage => _defaultLanguage;
  public static string CurrentLanguage { get { _ensureInitialized(); return _languages[0]; } }
  public static CultureInfo CurrentCulture { get { _ensureInitialized(); return _culture; } }
  public static IReadOnlyList<string> Languages { get { _ensureInitialized(); return _languages; } }

  public static event Action<Assembly, string>? MissingTranslation;

  public static IReadOnlyCollection<string> AvailableLanguages {
    get {
      _ensureInitialized();

      var languages = new HashSet<string>(
        _fileProvider!.GetAvailableLanguages(_assemblies.Keys),
        StringComparer.OrdinalIgnoreCase) {

        _defaultLanguage
      };

      return [.. languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }
  }

  public static void Register(Assembly assembly, ReadOnlySpan<(string Key, string Value)> translations) {
    var defaults = _tryGetOrCreateDic(assembly);

    foreach (var (key, value) in translations) {
      if (!defaults.TryAdd(key, value))
        throw new InvalidOperationException(
          $"Duplicate translation key '{key}' in assembly '{assembly.GetName().Name}'.");
    }
  }

  private static Dictionary<string, string> _tryGetOrCreateDic(Assembly assembly) {
    if (!_assemblies.TryGetValue(assembly, out var defaults)) {
      defaults = new Dictionary<string, string>(StringComparer.Ordinal);
      _assemblies.Add(assembly, defaults);
    }

    return defaults;
  }

  public static void Initialize(string? languages, ILanguageFileProvider fileProvider, string defaultLanguage = "en") {
    if (_fileProvider != null)
      throw new InvalidOperationException("Localization is already initialized.");

    _defaultLanguage = CultureInfo.GetCultureInfo(defaultLanguage).Name;

    if (_defaultLanguage.Length == 0)
      throw new ArgumentException("Invalid default language.", nameof(defaultLanguage));

    _languages = _parseLanguages(languages, _defaultLanguage);
    _culture = CultureInfo.GetCultureInfo(_languages[0]);
    _fileProvider = fileProvider;
  }

  public static string Get(Assembly assembly, string key) {
    _ensureInitialized();

    var defaults = _tryGetOrCreateDic(assembly);

    foreach (string language in _languages) {
      if (_tryGetTranslation(assembly, defaults, language, key, null, out string? value))
        return value!;
    }

    _reportMissing(assembly, key);
    return key;
  }

  public static string Format(Assembly assembly, string key, params object?[] args) {
    _ensureInitialized();
    return string.Format(_culture, Get(assembly, key), args);
  }

  public static string FormatPlural(Assembly assembly, string key, int count, params object?[] args) {
    _ensureInitialized();

    if (!_assemblies.TryGetValue(assembly, out var defaults))
      throw new InvalidOperationException($"Assembly '{assembly.GetName().Name}' is not registered.");

    foreach (string language in _languages) {
      string category = _getPluralCategory(language, count);

      if (_tryGetTranslation(assembly, defaults, language, key, category, out string? value))
        return string.Format(_culture, value!, [count, .. args]);
    }

    _reportMissing(assembly, key);

    return string.Format(_culture, key, [count, .. args]);
  }

  public static LanguageValidationResult Validate(Assembly assembly, string language, bool userOverride = false) {
    _ensureInitialized();

    if (!_assemblies.TryGetValue(assembly, out var defaults))
      throw new InvalidOperationException($"Assembly '{assembly.GetName().Name}' is not registered.");

    language = CultureInfo.GetCultureInfo(language).Name;

    IReadOnlyDictionary<string, string> translations;

    if (!userOverride && string.Equals(language, _defaultLanguage, StringComparison.OrdinalIgnoreCase))
      translations = defaults;
    else
      translations = _getCatalog(assembly, language, userOverride).Values;

    var missing = new List<string>();
    var unknown = new List<string>();

    foreach (string key in defaults.Keys) {
      if (translations.ContainsKey(key))
        continue;

      bool hasPluralForms = _pluralCategories.Any(
        category => translations.ContainsKey($"{key}.{category}"));

      if (!hasPluralForms) {
        missing.Add(key);
        continue;
      }

      // Without an unsuffixed fallback, all categories used by this language are required.
      foreach (string category in _getSupportedPluralCategories(language)) {
        if (!translations.ContainsKey($"{key}.{category}"))
          missing.Add($"{key}.{category}");
      }
    }

    foreach (string key in translations.Keys) {
      if (defaults.ContainsKey(key) || _isKnownPluralKey(key, defaults))
        continue;

      unknown.Add(key);
    }

    return new LanguageValidationResult {
      Language = language,
      MissingKeys = missing,
      UnknownKeys = unknown
    };
  }

  public static IReadOnlyDictionary<string, string> ExportTranslations(string language, string? description = null) {
    _ensureInitialized();

    var result = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var pair in _assemblies.OrderBy(x => x.Key.GetName().Name, StringComparer.Ordinal))
      result.Add(
        pair.Key.GetName().Name!,
        _exportTranslation(pair.Key, pair.Value, language, description));

    return result;
  }

  public static string ExportTranslation(Assembly assembly, string language, string? description = null) {
    _ensureInitialized();

    if (!_assemblies.TryGetValue(assembly, out var defaults))
      throw new InvalidOperationException(
        $"Assembly '{assembly.GetName().Name}' is not registered.");

    return _exportTranslation(assembly, defaults, language, description);
  }

  private static string _exportTranslation(
    Assembly assembly,
    IReadOnlyDictionary<string, string> defaults,
    string language,
    string? description) {

    language = CultureInfo.GetCultureInfo(language).Name;

    var builder = new StringBuilder();

    builder.Append("# Translation for ");
    builder.AppendLine(assembly.GetName().Name);

    if (!string.IsNullOrWhiteSpace(description)) {
      builder.Append("# ");
      builder.AppendLine(description);
    }

    builder.Append("# Language: ");
    builder.AppendLine(language);
    builder.AppendLine();

    foreach (var pair in defaults.OrderBy(
      x => x.Key, StringComparer.Ordinal)) {
      builder.Append(pair.Key);
      builder.Append('=');
      builder.AppendLine(LanguageCatalog.Escape(pair.Value));
    }

    return builder.ToString();
  }

  private static bool _tryGetTranslation(
    Assembly assembly,
    IReadOnlyDictionary<string, string> defaults,
    string language,
    string key,
    string? category,
    out string? value) {

    string pluralKey = category == null ? key : $"{key}.{category}";

    // User translations take precedence, including their unsuffixed fallback.
    var user = _getCatalog(assembly, language, true);

    if (user.Values.TryGetValue(pluralKey, out value))
      return true;

    if (category != null && user.Values.TryGetValue(key, out value))
      return true;

    if (string.Equals(language, _defaultLanguage, StringComparison.OrdinalIgnoreCase)) {
      // The default dictionary is the canonical source of translations.
      if (defaults.TryGetValue(pluralKey, out value))
        return true;

      if (category != null && defaults.TryGetValue(key, out value))
        return true;
    }
    else {
      var official = _getCatalog(assembly, language, false);

      if (official.Values.TryGetValue(pluralKey, out value))
        return true;

      if (category != null && official.Values.TryGetValue(key, out value))
        return true;
    }

    value = null;
    return false;
  }

  private static string _getPluralCategory(string language, int count) {
    if (language.StartsWith("cs", StringComparison.OrdinalIgnoreCase)) {
      if (count == 1) return "one";
      if (count >= 2 && count <= 4) return "few";
      return "other";
    }

    if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
      return count == 1 ? "one" : "other";

    // Currently unsupported languages use only "other".
    return "other";
  }

  private static string[] _getSupportedPluralCategories(string language) {
    if (language.StartsWith("cs", StringComparison.OrdinalIgnoreCase))
      return ["one", "few", "other"];

    if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
      return ["one", "other"];

    return ["other"];
  }

  private static bool _isKnownPluralKey(string key, IReadOnlyDictionary<string, string> defaults) {
    int separator = key.LastIndexOf('.');

    if (separator < 0) return false;

    string category = key[(separator + 1)..];

    return Array.IndexOf(_pluralCategories, category) >= 0
      && defaults.ContainsKey(key[..separator]);
  }

  private static void _reportMissing(Assembly assembly, string key) {
    if (_reportedMissing.Add((assembly, key)))
      MissingTranslation?.Invoke(assembly, key);
  }

  private static LanguageCatalog _getCatalog(Assembly assembly, string language, bool user) {
    var key = (assembly, language, user);

    if (_catalogs.TryGetValue(key, out var catalog))
      return catalog;

    using var stream = _fileProvider!.Open(assembly, language, user);

    catalog = stream == null
      ? new LanguageCatalog()
      : LanguageCatalog.Load(stream);

    _catalogs.Add(key, catalog);

    return catalog;
  }

  private static string[] _parseLanguages(string? languages, string defaultLanguage) {
    if (string.IsNullOrWhiteSpace(languages))
      languages = CultureInfo.CurrentUICulture.Name;

    var result = new List<string>();
    var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (string item in languages.Split(',')) {
      string language = item.Trim();

      if (language.Length == 0) continue;

      _addCulture(CultureInfo.GetCultureInfo(language));
    }

    _addCulture(CultureInfo.GetCultureInfo(defaultLanguage));

    return [.. result];

    void _addCulture(CultureInfo culture) {
      while (!culture.Equals(CultureInfo.InvariantCulture)) {
        if (unique.Add(culture.Name))
          result.Add(culture.Name);

        culture = culture.Parent;
      }
    }
  }

  private static void _ensureInitialized() {
    if (_fileProvider == null)
      throw new InvalidOperationException("Localization.Initialize() must be called first.");
  }
}