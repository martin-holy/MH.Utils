using System;

namespace MH.Utils.Imaging.Jpeg;

[Flags]
public enum RemoveMetadataOptions {
  None = 0,
  Exif = 1,
  Xmp = 2,
  Thumbnail = 4,
  All = Exif | Xmp | Thumbnail
}