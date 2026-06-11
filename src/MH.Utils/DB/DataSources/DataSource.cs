using System;

namespace MH.Utils.DB.DataSources;

public interface IDataSource {
  public string Name { get; }

  public void Load();
  public bool Save();
}

public interface IRelationDataSource : IDataSource;

public class DataSource(string name) : IDataSource {
  public string Name { get; } = name;

  public virtual void Load() => throw new NotImplementedException();
  public virtual bool Save() => throw new NotImplementedException();
}