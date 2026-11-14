using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MH.Utils.Localization;

public sealed class EmbeddedLanguageFileProvider(string directory) : ILanguageFileProvider {
  private readonly string _directory = Path.GetFullPath(directory);

  public IReadOnlyCollection<string> GetAvailableLanguages(IEnumerable<Assembly> assemblies) {
    var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var assembly in assemblies.Distinct()) {
      string assemblyName = _getAssemblyName(assembly);
      string prefix = assemblyName + ".";

      foreach (string resourceName in assembly.GetManifestResourceNames()) {
        if (resourceName.StartsWith(prefix, StringComparison.Ordinal) &&
            resourceName.EndsWith(".lang", StringComparison.Ordinal)) {
          string language = resourceName[prefix.Length..^5];

          if (_tryNormalizeLanguage(language, out string normalized))
            languages.Add(normalized);
        }
      }

      if (!Directory.Exists(_directory))
        continue;

      foreach (string path in Directory.EnumerateFiles(
        _directory, prefix + "*.lang")) {
        string fileName = Path.GetFileName(path);
        string language = fileName[prefix.Length..^5];

        if (language.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
          language = language[..^5];

        if (_tryNormalizeLanguage(language, out string normalized))
          languages.Add(normalized);
      }
    }

    return [.. languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
  }

  public Stream? Open(Assembly assembly, string language, bool userOverride) {
    string assemblyName = _getAssemblyName(assembly);

    if (userOverride) {
      string userPath = Path.Combine(_directory, $"{assemblyName}.{language}.user.lang");

      return _openFile(userPath);
    }

    // Embedded resources take precedence over external official files.
    string resourceName = $"{assemblyName}.{language}.lang";
    Stream? resource = assembly.GetManifestResourceStream(resourceName);

    if (resource != null)
      return resource;

    string path = Path.Combine(_directory, $"{assemblyName}.{language}.lang");

    return _openFile(path);
  }

  private static FileStream? _openFile(string path) {
    if (!File.Exists(path)) return null;

    return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
  }

  private static string _getAssemblyName(Assembly assembly) =>
    assembly.GetName().Name
    ?? throw new InvalidOperationException("Assembly has no name.");

  private static bool _tryNormalizeLanguage(string language, out string normalized) {
    try {
      normalized = CultureInfo.GetCultureInfo(language).Name;
      return normalized.Length > 0;
    }
    catch (CultureNotFoundException) {
      normalized = "";
      return false;
    }
  }
}