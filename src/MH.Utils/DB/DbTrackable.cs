namespace MH.Utils.DB;

public interface IDbTrackable {
  bool IsModified { get; set; }
  bool ArePropsModified { get; set; }
  int ChangesCount { get; }
}

public class DbTrackable : IDbTrackable {
  private bool _isModified;

  public bool IsModified { get => _isModified; set => _setIsModified(value); }
  public bool ArePropsModified { get; set; }
  public int ChangesCount { get; private set; }

  private void _setIsModified(bool value) {
    _isModified = value;

    if (value)
      ChangesCount++;
    else
      ChangesCount = 0;
  }
}