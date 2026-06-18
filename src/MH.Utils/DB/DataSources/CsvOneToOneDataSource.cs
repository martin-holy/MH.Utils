using MH.Utils.DB.Relations;
using System;
using System.Collections.Generic;

namespace MH.Utils.DB.DataSources;

public class CsvOneToOneDataSource<TA, TB>(
  SimpleDB db, string name, ICsvRepositoryDataSource<TA> dsA, ICsvRepositoryDataSource<TB> dsB, IRelation relation)
  : CsvDataSource<KeyValuePair<TA, TB>, NoLinkInfo>(db, name, 2), IRelationDataSource
  where TA : class where TB : class {

  public ICsvRepositoryDataSource<TA> DataSourceA { get; } = dsA;
  public ICsvRepositoryDataSource<TB> DataSourceB { get; } = dsB;
  public IRelation Relation { get; } = relation;

  protected virtual void _link(KeyValuePair<TA, TB> item) => throw new NotImplementedException();

  protected override void _parseLine(string line) =>
    _link(_fromCsv(line.AsSpan()).item);

  protected override (KeyValuePair<TA, TB> item, NoLinkInfo linkInfo) _fromCsv(ReadOnlySpan<char> csv) {
    int start = 0;
    int field = 0;

    TA? key = null;
    TB? value = null;

    for (int i = 0; i <= csv.Length; i++) {
      if (i == csv.Length || csv[i] == '|') {
        var slice = csv[start..i];

        switch (field) {
          case 0: key = DataSourceA.GetById(IdParser.Parse(slice)); break;
          case 1: value = DataSourceB.GetById(IdParser.Parse(slice)); break;
        }

        field++;
        start = i + 1;
      }
    }

    _validateFieldsCount(field, csv);

    return (new KeyValuePair<TA, TB>(key!, value!), new NoLinkInfo());
  }

  protected override string _toCsv(KeyValuePair<TA, TB> item) =>
    string.Join("|", item.Key.GetHashCode().ToString(), item.Value.GetHashCode().ToString());
}