using MH.Utils.Imaging.IsoBmff;
using MH.Utils.Imaging.Xmp;
using MH.Utils.IO;
using System;
using System.IO;
using System.Linq;

namespace MH.Utils.Imaging;

public sealed class VideoMetadata : IDisposable {
  public IsoBmffFile IsoBmff { get; }

  public int Width => IsoBmff.Width;
  public int Height => IsoBmff.Height;
  public int Orientation => IsoBmff.Orientation;
  public double? FrameRate => IsoBmff.FrameRate;
  public TimeSpan? Duration => IsoBmff.Duration;
  public string? Comment { get => _getComment(); set => _setComment(value); }
  public string[]? Keywords { get => _getKeywords(); set => _setKeywords(value); }
  public int? Rating { get => _getRating(); set => _setRating(value); }
  public MpRegionCollection? People => _getPeople();

  public bool IsXmpModified => IsoBmff.Xmp.Doc?.IsModified == true;
  public bool IsIsoBmffModified => IsoBmff._metadata._editor.IsModified;
  public bool IsModified => IsXmpModified || IsIsoBmffModified;

  public VideoMetadata(string filePath) {
    IsoBmff = new IsoBmffFile(filePath);
  }

  public VideoMetadata(Stream stream) {
    IsoBmff = new IsoBmffFile(stream);
  }

  private string? _getComment() =>
    IsoBmff.Xmp.GetComment()
      ?? IsoBmff._metadata.GetItemListComment()
      ?? IsoBmff._metadata.GetKeysComment();

  private void _setComment(string? value) {
    if (Comment == value) return;

    if (IsoBmff.Xmp.Doc != null || value != null)
      IsoBmff.Xmp.SetComment(value);

    if (IsoBmff._metadata.GetItemListComment() != null)
      IsoBmff._metadata.SetItemListComment(value);

    if (IsoBmff._metadata.GetKeysComment() != null)
      IsoBmff._metadata.SetKeysComment(value);
  }

  private string[]? _getKeywords() =>
    IsoBmff.Xmp.GetKeywords() ?? IsoBmff._metadata.GetKeywords();

  private void _setKeywords(string[]? value) {
    if ((Keywords ?? []).SequenceEqual(value ?? [])) return;

    if (IsoBmff.Xmp.Doc != null || value != null)
      IsoBmff.Xmp.SetKeywords(value);

    var legacyValue = value is { Length: > 0 }
      ? string.Join(", ", value)
      : null;

    if (IsoBmff._metadata.GetItemListKeywords() != null)
      IsoBmff._metadata.SetItemListKeywords(legacyValue);

    if (IsoBmff._metadata.GetKeysKeywords() != null)
      IsoBmff._metadata.SetKeysKeywords(legacyValue);
  }

  private int? _getRating() =>
    IsoBmff.Xmp.GetRating();

  private void _setRating(int? value) {
    if (Rating == value) return;

    IsoBmff.Xmp.SetRating(value);
  }

  private MpRegionCollection? _getPeople() =>
    IsoBmff.Xmp.GetPeople();

  public IBinaryWritePlan CreateWritePlan() =>
    IsoBmff.CreateWritePlan();

  public void Write(IBinaryWritePlan plan) =>
    IsoBmff.Write(plan);

  public void Dispose() => 
    IsoBmff.Dispose();
}