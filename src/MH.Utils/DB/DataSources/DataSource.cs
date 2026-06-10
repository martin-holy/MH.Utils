using System;

namespace MH.Utils.DB.DataSources;

public class DataSource(string name) : IDataSource {
  public string Name { get; } = name;

  public virtual void Load() => throw new NotImplementedException();
  public virtual bool Save() => throw new NotImplementedException();
}