namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffMetadataEntry(string key, string value, IsoBmffBox? item) {
  public string Key { get; } = key;
  public string Value { get; set; } = value;

  internal IsoBmffBox? Item { get; } = item;
}