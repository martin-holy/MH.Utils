using System;
using System.Collections.Generic;

namespace MH.Utils.DB.Interfaces;

[Obsolete("Use DB.Repositories.Repository")]
public interface IRepository {
  public bool IsModified { get; set; }
}

[Obsolete("Use DB.Repositories.Repository")]
public interface IRepository<T> : IRepository {
  public event EventHandler<T> ItemCreatedEvent;
  public event EventHandler<T> ItemUpdatedEvent;
  public event EventHandler<T> ItemDeletedEvent;
  public event EventHandler<IList<T>> ItemsDeletedEvent;
}