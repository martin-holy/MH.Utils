using System.Collections.Generic;

namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffItemList(IsoBmffReader reader, IsoBmffBox ilst, bool isNew = false) {
  private List<IsoBmffMetadataEntry>? _entries = isNew ? [] : null;

  public static IsoBmffItemList? Find(IsoBmffReader reader, IsoBmffBox moov) {
    if (reader.FindChild(moov, IsoBmffTypes.Udta) is not { } udta) return null;
    if (reader.FindChild(udta, IsoBmffTypes.Meta) is not { } meta) return null;
    if (reader.FindChild(meta, IsoBmffTypes.Ilst, reader.GetBoxChildrenOffset(meta)) is not { } ilst) return null;
    return new IsoBmffItemList(reader, ilst);
  }

  public static IsoBmffItemList Create(IsoBmffReader reader, List<IsoBmffNode> editedNodes) {
    var moovIndex = editedNodes.FindIndex(IsoBmffTypes.Moov, throwIfNotFound: true);
    var udtaIndex = editedNodes.FindOrInsert(IsoBmffTypes.Udta, 8, moovIndex);
    var metaIndex = editedNodes.FindOrInsert(IsoBmffTypes.Meta, 12, udtaIndex, new byte[4]);
    var ilstIndex = editedNodes.FindOrInsert(IsoBmffTypes.Ilst, 8, metaIndex);
    var ilst = editedNodes[ilstIndex].Box;

    return new IsoBmffItemList(reader, ilst, isNew: true);
  }

  public string? GetKeywords() =>
    _getValue(IsoBmffTypes.Keyw);

  public string? GetComment() =>
    _getValue(IsoBmffTypes.Comment);

  private string? _getValue(uint type) {
    var entries = _getEntries();

    foreach (var entry in entries) {
      if (entry.Item?.Type == type)
        return entry.Value;
    }

    return null;
  }

  private List<IsoBmffMetadataEntry> _getEntries() {
    if (_entries is not null) return _entries;

    var result = new List<IsoBmffMetadataEntry>();

    reader._stream.Position = ilst.DataOffset;

    while (reader._stream.Position < ilst.End) {
      if (reader.ReadBox(ilst.End) is not { } item) break;

      if (reader.ReadDataString(item) is { } value)
        result.Add(new IsoBmffMetadataEntry(item.TypeName, value, item));

      reader._stream.Position = item.End;
    }

    return _entries = result;
  }

  public void SetKeywords(string? value, IsoBmffEditor editor) =>
    _setValue(IsoBmffTypes.Keyw, value, editor);

  public void SetComment(string? value, IsoBmffEditor editor) =>
    _setValue(IsoBmffTypes.Comment, value, editor);

  private void _setValue(uint type, string? value, IsoBmffEditor editor) {
    var key = IsoBmffTypes.GetTypeName(type);
    var entries = _getEntries();

    var entryIndex = entries.FindIndex(x => x.Item?.Type == type);
    if (entryIndex >= 0) {
      var entry = entries[entryIndex];
      if (value == null) {
        editor.RemoveEntry(entry, key);
        entries.RemoveAt(entryIndex);
      }
      else {
        editor.UpdateEntry(entry, value, key);
      }

      return;
    }

    if (value == null) return;

    entries.Add(editor.AddEntry(key, value, type, ilst));
  }
}