namespace MH.Utils.Imaging.IsoBmff;

internal sealed record IsoBmffMove(long SourceOffset, long DestinationOffset, long Length);