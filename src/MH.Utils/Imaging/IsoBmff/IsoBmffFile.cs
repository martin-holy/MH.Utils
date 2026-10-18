using MH.Utils.Imaging.Xmp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MH.Utils.Imaging.IsoBmff;

public sealed class IsoBmffFile : IDisposable {
  private readonly string? _filePath;
  private readonly bool _ownsStream;
  private Stream _stream;
  internal IsoBmffReader _reader;

  private bool _xmpRead;
  private IsoBmffMetadataEntry? _xmpEntry;
  private IsoBmffBoxNode? _xmpNode;
  private XmpMetadata? _xmp;
  internal IsoBmffMetadata _metadata;

  public int Width => _metadata.Width;
  public int Height => _metadata.Height;
  public int Orientation => _metadata.Orientation;
  public double? FrameRate => _metadata.FrameRate;
  public TimeSpan? Duration => _metadata.Duration;
  public XmpMetadata Xmp => _getXmp();
  public string? FilePath => _filePath;

  public IsoBmffFile(string filePath) {
    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

    _filePath = Path.GetFullPath(filePath);
    _stream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    _ownsStream = true;

    _reader = new IsoBmffReader(_stream);
    _metadata = new IsoBmffMetadata(_stream, _reader);
  }

  public IsoBmffFile(Stream stream) {
    ArgumentNullException.ThrowIfNull(stream);

    _stream = stream;
    _reader = new IsoBmffReader(_stream);
    _metadata = new IsoBmffMetadata(_stream, _reader);
  }

  internal XmpMetadata _getXmp() {
    if (_xmp != null) return _xmp;
    if (!_xmpRead) {
      _xmpEntry = IsoBmffXmp.Find(_stream, _reader);

      if (_xmpEntry != null)
        _xmp = new XmpMetadata(_xmpEntry.Value);

      _xmpRead = true;
    }

    return _xmp ??= new XmpMetadata(null);
  }

  internal List<IsoBmffBoxNode> _getEditedBoxes() {
    var boxes = _metadata._editor.GetEditedBoxes();

    if (_xmp?.Doc is not { IsModified: true })
      return boxes;

    _xmpNode = IsoBmffXmp.UpdateNode(_xmpEntry, _xmpNode, _xmp.ToPacket(), boxes, _stream);

    return boxes;
  }

  public void Save() {
    var savePlan = _createSavePlan();

    if (!savePlan.Plan.RequiresFullRewrite &&
        savePlan.Diff!.Moves.Count == 0 &&
        savePlan.Diff.Writes.Count == 0)
      return;

    if (_filePath is not null)
      _saveToPath(savePlan, _filePath);
    else
      _saveToStreamBacked(savePlan);
  }

  private _savePlan _createSavePlan() {
    if (!_stream.CanRead || !_stream.CanSeek)
      throw new InvalidOperationException("Saving requires a readable, seekable source stream.");

    var originalBoxes = _metadata._editor._getOriginalBoxes();
    var editedBoxes = _getEditedBoxes();
    var planner = new IsoBmffLayoutPlanner(
      originalBoxes,
      editedBoxes,
      _reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var plan = planner.CreateEditedLayout(original, editedBoxes);
    var diff = plan.RequiresFullRewrite
      ? null
      : IsoBmffLayoutDiffer.Diff(
        original,
        plan.Layout,
        editedBoxes.Where(x => x.Data is not null));

    return new _savePlan(originalBoxes, editedBoxes, original, plan, diff);
  }

  private void _saveToPath(_savePlan savePlan, string filePath) {
    if (!savePlan.Plan.RequiresFullRewrite) {
      _saveToStream(_stream, savePlan);
      _stream.Flush();
      _reloadAfterSave();
      return;
    }

    var directory = Path.GetDirectoryName(filePath)!;
    var temporaryPath = _createTemporaryPath(directory, Path.GetFileName(filePath));

    try {
      using (var temporary = new FileStream(
        temporaryPath,
        FileMode.CreateNew,
        FileAccess.ReadWrite,
        FileShare.None)) {

        _saveToStream(temporary, savePlan);
        temporary.Flush(flushToDisk: true);
      }

      _stream.Dispose();

      try {
        File.Move(temporaryPath, filePath, overwrite: true);
      }
      catch {
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        throw;
      }

      _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
      _reloadAfterSave();
    }
    finally {
      _deleteTemporaryFile(temporaryPath);
    }
  }

  private void _saveToStreamBacked(_savePlan savePlan) {
    if (!_stream.CanWrite)
      throw new InvalidOperationException("Saving to a stream requires a writable stream.");

    if (!savePlan.Plan.RequiresFullRewrite) {
      _saveToStream(_stream, savePlan);
      _stream.Flush();
      _reloadAfterSave();
      return;
    }

    var temporaryPath = _createTemporaryPath(Path.GetTempPath(), "isobmff");

    try {
      using (var temporary = new FileStream(
        temporaryPath,
        FileMode.CreateNew,
        FileAccess.ReadWrite,
        FileShare.None)) {
        _saveToStream(temporary, savePlan);
        temporary.Flush(flushToDisk: true);
        temporary.Position = 0;

        _stream.Position = 0;
        temporary.CopyTo(_stream);
        _stream.SetLength(temporary.Length);
        _stream.Flush();
      }

      _reloadAfterSave();
    }
    finally {
      _deleteTemporaryFile(temporaryPath);
    }
  }

  private void _saveToStream(Stream destination, _savePlan savePlan) {
    if (!destination.CanWrite || !destination.CanSeek)
      throw new InvalidOperationException("The destination stream must be writable and seekable.");

    if (savePlan.Plan.RequiresFullRewrite) {
      IsoBmffLayoutExecutor.ExecuteFullRewrite(
        _stream,
        destination,
        savePlan.OriginalBoxes,
        savePlan.EditedBoxes,
        savePlan.Plan.Layout,
        _reader.GetBoxChildrenOffset);
    }
    else {
      IsoBmffLayoutExecutor.ExecuteMoves(destination, savePlan.Diff!.Moves);
      IsoBmffLayoutExecutor.ExecuteWrites(destination, savePlan.Diff.Writes);
    }
  }

  public void Dispose() {
    if (_ownsStream)
      _stream.Dispose();
  }

  private void _reloadAfterSave() {
    _reader = new IsoBmffReader(_stream);
    _metadata = new IsoBmffMetadata(_stream, _reader);

    _xmpRead = false;
    _xmpEntry = null;
    _xmpNode = null;
    _xmp = null;
  }

  private static string _createTemporaryPath(string directory, string name) =>
    Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.tmp");

  private static void _deleteTemporaryFile(string path) {
    try {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch {
    }
  }

  private sealed record _savePlan(
    List<IsoBmffBoxNode> OriginalBoxes,
    List<IsoBmffBoxNode> EditedBoxes,
    IsoBmffLayout Original,
    IsoBmffLayoutPlan Plan,
    IsoBmffLayoutDiff? Diff);
}