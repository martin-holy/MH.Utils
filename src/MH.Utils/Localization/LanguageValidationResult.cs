using System.Collections.Generic;

namespace MH.Utils.Localization;

public sealed class LanguageValidationResult {
  public required string Language { get; init; }
  public required IReadOnlyCollection<string> MissingKeys { get; init; }
  public required IReadOnlyCollection<string> UnknownKeys { get; init; }

  public bool IsValid => MissingKeys.Count == 0 && UnknownKeys.Count == 0;
}