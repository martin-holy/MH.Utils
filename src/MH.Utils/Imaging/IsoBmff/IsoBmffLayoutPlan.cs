namespace MH.Utils.Imaging.IsoBmff;

internal sealed class IsoBmffLayoutPlan(IsoBmffLayout layout, bool requiresFullRewrite) {
  public IsoBmffLayout Layout { get; } = layout;
  public bool RequiresFullRewrite { get; } = requiresFullRewrite;
}