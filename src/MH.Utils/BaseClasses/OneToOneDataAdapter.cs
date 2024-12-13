using MH.Utils.Interfaces;
using System.Collections.Generic;

namespace MH.Utils.BaseClasses;

public class OneToOneDataAdapter<TA, TB>(SimpleDB db, string name, TableDataAdapter<TA> daA, TableDataAdapter<TB> daB)
  : DataAdapter<KeyValuePair<TA, TB>>(db, name, 2), IRelationDataAdapter
  where TA : class where TB : class {

  public TableDataAdapter<TA> DataAdapterA { get; } = daA;
  public TableDataAdapter<TB> DataAdapterB { get; } = daB;

  protected override KeyValuePair<TA, TB> _fromCsv(string[] csv) =>
    new(DataAdapterA.GetById(csv[0])!, DataAdapterB.GetById(csv[1])!);

  protected override string _toCsv(KeyValuePair<TA, TB> item) =>
    string.Join("|", item.Key.GetHashCode().ToString(), item.Value.GetHashCode().ToString());
}