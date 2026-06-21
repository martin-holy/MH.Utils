using MH.Utils.DB.Repositories;
using System;
using System.Collections.Generic;

namespace MH.Utils.DB.DataSources;

public interface ICsvRepositoryDataSource : ICsvDataSource, IRepositoryDataSource {
  public void LinkReferences();
  public void FillRepository();
}

public interface ICsvRepositoryDataSource<T> : ICsvRepositoryDataSource {
  public new IRepository<T> Repository { get; }
  public T? GetById(int id, bool nullable = false);
}

public abstract class CsvRepositoryDataSource<T, TR, TLinkInfo>(SimpleDB db, string name, int fieldsCount, TR repository)
  : CsvDataSource<T, TLinkInfo>(db, name, fieldsCount), ICsvRepositoryDataSource<T>
  where T : class where TR : IRepository<T> {

  private Dictionary<int, int>? _notFoundIds;

  protected List<(T, TLinkInfo)> _allLinkInfo = [];

  public TR Repository { get; } = repository;
  public Dictionary<int, T> AllDict { get; } = [];

  IRepository<T> ICsvRepositoryDataSource<T>.Repository => Repository;
  IRepository IRepositoryDataSource.Repository => Repository;

  public virtual void LinkReferences() { }

  public override bool Save() {
    var success = base.Save();
    if (success) Repository.IsModified = false;
    return success;
  }

  protected override void _parseLine(string line) =>
    _addItem(_fromCsv(line.AsSpan()));

  public void FillRepository() =>
    Repository.SetAll([.. AllDict.Values]);

  public override void Clear() {
    base.Clear();
    AllDict.Clear();
    AllDict.TrimExcess(0);
    _allLinkInfo.Clear();
    _allLinkInfo.TrimExcess();
  }

  protected virtual int _getKey(T item) =>
    item.GetHashCode();

  protected virtual void _addItem((T item, TLinkInfo linkInfo) data) {
    AllDict.Add(_getKey(data.item), data.item);
    if (data.linkInfo is not NoLinkInfo)
      _allLinkInfo.Add(data);
  }

  public static List<TI>? IdToRecord<TI>(string csv, Dictionary<int, TI> source, Func<int, TI?> resolveNotFound) {
    if (string.IsNullOrEmpty(csv)) return null;

    List<TI> result = [];

    CsvParser.ParseInts(csv, (source, resolveNotFound, result), static (state, id) => {
      if (!state.source.TryGetValue(id, out var rec))
        rec = state.resolveNotFound(id);

      if (rec != null)
        state.result.Add(rec);
    });

    return result.Count == 0 ? null : result;
  }

  public List<T>? LinkList(string csv) =>
    IdToRecord(csv, AllDict, notFoundId => null);

  public List<T>? LinkList(string csv, Func<int, T>? getNotFoundRecord, ICsvRepositoryDataSource seeker) =>
    IdToRecord(csv, AllDict, notFoundId => _resolveNotFoundRecord(notFoundId, getNotFoundRecord, seeker));

  protected T? _resolveNotFoundRecord(int notFoundId, Func<int, T>? getNotFoundRecord, ICsvRepositoryDataSource seeker) {
    if (getNotFoundRecord == null) return null;
    _notFoundIds ??= [];

    seeker.Repository.IsModified = true;

    if (_notFoundIds.TryGetValue(notFoundId, out var id)) return AllDict[id];

    var item = getNotFoundRecord(notFoundId);

    _notFoundIds.Add(notFoundId, _getKey(item));
    AllDict.Add(_getKey(item), item);
    return item;
  }

  public virtual T? GetById(int id, bool nullable = false) =>
    nullable && id == 0 ? null : AllDict[id];
}