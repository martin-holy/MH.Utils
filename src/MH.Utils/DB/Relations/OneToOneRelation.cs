using MH.Utils.DB.Repositories;

namespace MH.Utils.DB.Relations;

public class OneToOneRelation<TA, TB>(IRepository<TA> repoA, IRepository<TB> repoB) : Relation, IRelation<TA, TB> {
  public IRepository<TA> RepositoryA { get; } = repoA;
  public IRepository<TB> RepositoryB { get; } = repoB;
}