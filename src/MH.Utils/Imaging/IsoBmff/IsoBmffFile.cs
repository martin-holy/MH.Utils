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
  private IsoBmffNode? _xmpNode;
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

  internal List<IsoBmffNode> _getEditedNodes() {
    var nodes = _metadata._editor.GetEditedNodes();

    if (_xmp?.Doc is not { IsModified: true })
      return nodes;

    _xmpNode = IsoBmffXmp.UpdateNode(_xmpEntry, _xmpNode, _xmp.ToPacket(), nodes, _stream);

    return nodes;
  }

  public void Write(IWritePlan plan) {
    if (plan is not WritePlan writePlan)
      throw new ArgumentException("Incompatible write plan.", nameof(plan));

    if (!writePlan.Plan.RequiresFullRewrite &&
        writePlan.Diff!.Moves.Count == 0 &&
        writePlan.Diff.Writes.Count == 0)
      return;

    if (_filePath is not null)
      _writeToPath(writePlan, _filePath);
    else
      _writeToStreamBacked(writePlan);
  }

  public IWritePlan CreateWritePlan() {
    if (!_stream.CanRead || !_stream.CanSeek)
      throw new InvalidOperationException("Writing requires a readable, seekable source stream.");

    var originalNodes = _metadata._editor._getOriginalNodes();
    var editedNodes = _getEditedNodes();
    var planner = new IsoBmffLayoutPlanner(originalNodes, editedNodes, _reader.GetBoxChildrenOffset);
    var original = planner.CreateOriginalLayout();
    var plan = planner.CreateEditedLayout(original, editedNodes);
    var diff = plan.RequiresFullRewrite
      ? null
      : IsoBmffLayoutDiffer.Diff(
        original,
        plan.Layout,
        editedNodes.Where(x => x.Data is not null));

    return new WritePlan(originalNodes, editedNodes, original, plan, diff);
  }

  private void _writeToPath(WritePlan writePlan, string filePath) {
    if (!writePlan.Plan.RequiresFullRewrite) {
      _writeToStream(_stream, writePlan);
      _stream.Flush();
      _reloadAfterWrite();
      return;
    }

    var directory = Path.GetDirectoryName(filePath)!;
    var temporaryPath = _createTemporaryPath(directory, Path.GetFileName(filePath));

    try {
      using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
        _writeToStream(temporary, writePlan);
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
      _reloadAfterWrite();
    }
    finally {
      _deleteTemporaryFile(temporaryPath);
    }
  }

  private void _writeToStreamBacked(WritePlan writePlan) {
    if (!_stream.CanWrite)
      throw new InvalidOperationException("Writing to a stream requires a writable stream.");

    if (!writePlan.Plan.RequiresFullRewrite) {
      _writeToStream(_stream, writePlan);
      _stream.Flush();
      _reloadAfterWrite();
      return;
    }

    var tmpPath = _createTemporaryPath(Path.GetTempPath(), "isobmff");

    try {
      using (var tmp = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) {
        _writeToStream(tmp, writePlan);
        tmp.Flush(flushToDisk: true);
        tmp.Position = 0;

        _stream.Position = 0;
        tmp.CopyTo(_stream);
        _stream.SetLength(tmp.Length);
        _stream.Flush();
      }

      _reloadAfterWrite();
    }
    finally {
      _deleteTemporaryFile(tmpPath);
    }
  }

  private void _writeToStream(Stream destination, WritePlan writePlan) {
    if (!destination.CanWrite || !destination.CanSeek)
      throw new InvalidOperationException("The destination stream must be writable and seekable.");

    if (writePlan.Plan.RequiresFullRewrite) {
      IsoBmffLayoutExecutor.ExecuteFullRewrite(
        _stream,
        destination,
        writePlan.OriginalNodes,
        writePlan.EditedNodes,
        writePlan.Plan.Layout,
        _reader.GetBoxChildrenOffset);
    }
    else {
      IsoBmffLayoutExecutor.ExecuteMoves(destination, writePlan.Diff!.Moves);
      IsoBmffLayoutExecutor.ExecuteWrites(destination, writePlan.Diff.Writes);
    }
  }

  public void Dispose() {
    if (_ownsStream)
      _stream.Dispose();
  }

  private void _reloadAfterWrite() {
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

  private sealed record WritePlan(
    List<IsoBmffNode> OriginalNodes,
    List<IsoBmffNode> EditedNodes,
    IsoBmffLayout Original,
    IsoBmffLayoutPlan Plan,
    IsoBmffLayoutDiff? Diff) : IWritePlan {

    public bool IsFullRewriteRequired => Plan.RequiresFullRewrite;
  }
}