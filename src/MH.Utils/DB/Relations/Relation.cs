using MH.Utils.DB.Repositories;

namespace MH.Utils.DB.Relations;

public interface IRelation : IDbTrackable;

public interface IRelation<TA, TB> : IRelation {
  public IRepository<TA> RepositoryA { get; }
  public IRepository<TB> RepositoryB { get; }
}

public class Relation : DbTrackable, IRelation;

public class Relation<TA, TB>(IRepository<TA> repoA, IRepository<TB> repoB) : Relation, IRelation<TA, TB> {
  public IRepository<TA> RepositoryA { get; } = repoA;
  public IRepository<TB> RepositoryB { get; } = repoB;
}