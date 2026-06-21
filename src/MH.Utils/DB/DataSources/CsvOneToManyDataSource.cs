using MH.Utils.DB.Relations;
using MH.Utils.Extensions;
using System;
using System.Collections.Generic;

namespace MH.Utils.DB.DataSources;

public abstract class CsvOneToManyDataSource<TA, TB>(
  SimpleDB db, string name, ICsvRepositoryDataSource<TA> keySource, IRelation relation)
  : CsvDataSource<KeyValuePair<TA, List<TB>>, NoLinkInfo>(db, name, 2), ICsvRelationDataSource
  where TA : class where TB : class {

  public ICsvRepositoryDataSource<TA> KeySource { get; } = keySource;
  public IRelation Relation { get; } = relation;

  public virtual TB? GetValueById(int id) => throw new NotImplementedException();

  protected virtual void _link(KeyValuePair<TA, List<TB>> item) => throw new NotImplementedException();

  protected override void _parseLine(string line) =>
    _link(_fromCsv(line.AsSpan()).item);

  protected override (KeyValuePair<TA, List<TB>> item, NoLinkInfo linkInfo) _fromCsv(ReadOnlySpan<char> csv) {
    int start = 0;
    int field = 0;

    TA? key = null;
    ReadOnlySpan<char> valueIds = [];

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == '|') {
        var slice = csv[start..i];

        switch (field) {
          case 0: key = KeySource.GetById(CsvParser.ParseInt(slice)); break;
          case 1: valueIds = slice; break;
        }

        field++;
        start = i + 1;
      }
    }

    _validateFieldsCount(field, csv);

    return (new KeyValuePair<TA, List<TB>>(key!, _getByIds(valueIds)), default);
  }

  protected override string _toCsv(KeyValuePair<TA, List<TB>> item) =>
    string.Join("|", item.Key.GetHashCode().ToString(), item.Value.ToHashCodes().ToCsv());

  private List<TB> _getByIds(ReadOnlySpan<char> ids) {
    if (ids.IsEmpty) return [];
    var result = new List<TB>();

    CsvParser.ParseInts(ids, (result, this), static (state, id) => {
      if (state.Item2.GetValueById(id) is { } value)
        state.result.Add(value);
    });

    return result;
  }
}