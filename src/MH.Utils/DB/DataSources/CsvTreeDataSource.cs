using MH.Utils.DB.Repositories;
using MH.Utils.Tree;
using System;

namespace MH.Utils.DB.DataSources;

public abstract class CsvTreeDataSource<T, TR, TLinkInfo>(SimpleDB db, string name, int fieldsCount, TR repository)
  : CsvRepositoryDataSource<T, TR, TLinkInfo>(db, name, fieldsCount, repository)
  where T : class, ITreeItem where TR : IRepository<T> {

  protected void _linkTree(ITreeItem root, Func<TLinkInfo, int> getParentId) {
    foreach (var (item, li) in _allLinkInfo) {
      if (item.Parent != null) continue;
      var parentId = getParentId(li);
      item.Parent = parentId == 0 ? root : AllDict[parentId];
      item.Parent.Items.Add(item);
    }
  }
}