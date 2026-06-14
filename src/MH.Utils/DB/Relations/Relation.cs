using MH.Utils.DB.Repositories;

namespace MH.Utils.DB.Relations;

public interface IRelation {
  bool IsModified { get; set; }
  bool AreRelationPropsModified { get; set; }
  int ChangesCount { get; }
}

public interface IRelation<TA, TB> : IRelation {
  public IRepository<TA> RepositoryA { get; }
  public IRepository<TB> RepositoryB { get; }
}

public class Relation : IRelation {
  private bool _isModified;

  public bool IsModified { get => _isModified; set => _setIsModified(value); }
  public bool AreRelationPropsModified { get; set; }
  public int ChangesCount { get; private set; }

  private void _setIsModified(bool value) {
    _isModified = value;

    if (value)
      ChangesCount++;
    else
      ChangesCount = 0;
  }
}