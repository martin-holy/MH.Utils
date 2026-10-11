namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffMetadataEntry(string key, string value, IsoBmffBox? item, IsoBmffBoxNode? node = null) {
  public string Key { get; } = key;
  public string Value { get; set; } = value;

  internal IsoBmffBox? Item { get; } = item;
  internal IsoBmffBoxNode? Node { get; set; } = node;
}