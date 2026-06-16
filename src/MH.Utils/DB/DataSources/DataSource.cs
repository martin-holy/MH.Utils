using MH.Utils.DB.Relations;
using MH.Utils.DB.Repositories;
using System;

namespace MH.Utils.DB.DataSources;

public interface IDataSource {
  public string Name { get; }

  public void Load();
  public bool Save();
  public void LoadProps();
  public bool SaveProps();
}

public interface IRepositoryDataSource: IDataSource {
  IRepository Repository { get; }
}

public interface IRelationDataSource : IDataSource {
  IRelation Relation { get; }
}

public abstract class DataSource(string name) : IDataSource {
  public string Name { get; } = name;

  public virtual void Load() => throw new NotImplementedException();
  public virtual bool Save() => throw new NotImplementedException();
  public virtual void LoadProps() => throw new NotImplementedException();
  public virtual bool SaveProps() => throw new NotImplementedException();
}