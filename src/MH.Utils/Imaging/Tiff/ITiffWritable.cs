namespace MH.Utils.Imaging.Tiff;

public interface ITiffWritable {
  uint OriginalOffset { get; }
  uint WriteOffset { get; set; }
  int OriginalSize { get; }
  int CurrentSize { get; }

  void Write(TiffWriter writer);
}