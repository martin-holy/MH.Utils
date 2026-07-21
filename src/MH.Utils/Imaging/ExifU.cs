using MH.Utils.Imaging.Tiff;
using System;
using System.IO;

namespace MH.Utils.Imaging;

public static class ExifU {
  public static ReadOnlySpan<byte> ExifHeader => "Exif\0\0"u8;
  public static ReadOnlySpan<byte> AsciiHeader => "ASCII\0\0\0"u8;
  public static ReadOnlySpan<byte> UnicodeHeader => "UNICODE\0"u8;
  public static ReadOnlySpan<byte> JisHeader => "JIS\0\0\0\0\0"u8;

  public static bool WriteExifToJpeg(string srcPath, ImageMetadata metadata) {
    var tmpPath = srcPath + ".tmp";

    try {
      using (var input = File.OpenRead(srcPath))
      using (var output = File.Create(tmpPath)) {
        TiffWriter.WriteExifToJpeg(input, output, MetadataToTiff(metadata));
      }

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex, srcPath);

      try {
        if (File.Exists(tmpPath))
          File.Delete(tmpPath);
      }
      catch { }

      return false;
    }
  }

  public static byte[] MetadataToTiff(ImageMetadata metadata) {
    TiffFile? file;
    if (metadata.Reader == null) {
      file = TiffFile.CreateEmpty();
    }
    else {
      file = TiffParser.Parse(metadata.Reader);
      TiffResolver.Resolve(metadata.Reader, file);
    }
      
    TiffEditor.Apply(file, metadata);
    var layout = TiffLayoutBuilder.Build(file, metadata.Reader);
    TiffLayoutPlanner.Plan(layout);
    var tiff = TiffSerializer.Serialize(file, metadata.Reader?.IsLittleEndian ?? true);

    return tiff;
  }
}