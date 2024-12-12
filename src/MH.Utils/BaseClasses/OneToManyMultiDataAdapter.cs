using MH.Utils.Extensions;
using MH.Utils.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MH.Utils.BaseClasses;

public class OneToManyMultiDataAdapter<TA, TB> : DataAdapter<KeyValuePair<TA, List<TB>>>, IRelationDataAdapter where TA : class where TB : class {
  public new Dictionary<TA, List<TB>> All { get; } = [];

  protected TableDataAdapter<TA> _keyDataAdapter;

  public OneToManyMultiDataAdapter(SimpleDB db, string name, TableDataAdapter<TA> keyDa) :
    base(db, name, 2) {
    _keyDataAdapter = keyDa;

    _keyDataAdapter.ItemDeletedEvent += (_, e) => {
      if (All.TryGetValue(e, out var b))
        ItemDelete(new(e, b));
    };
  }

  protected override KeyValuePair<TA, List<TB>> _fromCsv(string[] csv) =>
    new(_keyDataAdapter.GetById(csv[0])!, GetByIds(csv[1]));

  protected override string _toCsv(KeyValuePair<TA, List<TB>> item) =>
    string.Join("|", item.Key.GetHashCode().ToString(), item.Value.ToHashCodes().ToCsv());

  protected override void _addItem(KeyValuePair<TA, List<TB>> item, string[] props) =>
    All.Add(item.Key, item.Value);

  public override KeyValuePair<TA, List<TB>> ItemCreate(KeyValuePair<TA, List<TB>> item) {
    All.Add(item.Key, item.Value);
    IsModified = true;
    _raiseItemCreated(item);
    _onItemCreated(this, item);
    return item;
  }

  public override void ItemDelete(KeyValuePair<TA, List<TB>> item, bool singleDelete = true) {
    All.Remove(item.Key);
    IsModified = true;
    _raiseItemDeleted(item);

    if (!singleDelete) return;
    var items = new[] { item };
    _raiseItemsDeleted(items);
    _onItemDeleted(this, item);
    _onItemsDeleted(this, items);
  }

  public virtual TB? GetValueById(string id) => throw new NotImplementedException();

  public List<TB> GetByIds(string ids) =>
    ids
      .Split(',')
      .Select(GetValueById)
      .Where(x => x != null)
      .Select(x => x!)
      .ToList();
}