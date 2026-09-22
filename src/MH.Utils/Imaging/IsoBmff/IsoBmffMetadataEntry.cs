namespace MH.Utils.Imaging.IsoBmff;

internal readonly struct IsoBmffMetadataEntry(string key, string value) {
  public string Key { get; } = key;
  public string Value { get; } = value;
}