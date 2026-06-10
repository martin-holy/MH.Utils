using System.Collections.Generic;

namespace MH.Utils.DB.DataSources;

public interface IDataSource {
  public string Name { get; }

  public void Load();
  public bool Save();
}

public interface IDataSource<T> : IDataSource {
  public IEnumerable<T> All { get; }
}