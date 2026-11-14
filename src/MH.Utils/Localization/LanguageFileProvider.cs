using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace MH.Utils.Localization;

public interface ILanguageFileProvider {
  IReadOnlyCollection<string> GetAvailableLanguages(IEnumerable<Assembly> assemblies);
  Stream? Open(Assembly assembly, string language, bool userOverride);
}