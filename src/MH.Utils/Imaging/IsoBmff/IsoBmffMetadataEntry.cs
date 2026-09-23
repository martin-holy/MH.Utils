namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffMetadataEntry(string key, string value, IsoBmffBox item, IsoBmffBox data) {
  public string Key { get; } = key;
  public string Value { get; } = value;

  internal IsoBmffBox Item { get; } = item;
  internal IsoBmffBox Data { get; } = data;
}