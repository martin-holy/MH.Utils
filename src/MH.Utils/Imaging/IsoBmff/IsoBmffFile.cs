using MH.Utils.Imaging.Xmp;
using MH.Utils.IO;
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

  public void Write(IBinaryWritePlan plan) {
    if (!plan.RequiresFullRewrite &&
        plan.Moves.Count == 0 &&
        plan.Writes.Count == 0)
      return;

    if (_filePath is not null)
      _stream = BinaryInPlaceWriter.Write(_stream, _filePath, plan);
    else
      BinaryInPlaceWriter.Write(_stream, plan);

    _reloadAfterWrite();
  }

  public IBinaryWritePlan CreateWritePlan() {
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

  private sealed record WritePlan(
    List<IsoBmffNode> OriginalNodes,
    List<IsoBmffNode> EditedNodes,
    IsoBmffLayout Original,
    IsoBmffLayoutPlan Plan,
    IsoBmffLayoutDiff? Diff) : IBinaryWritePlan {

    public bool RequiresFullRewrite => Plan.RequiresFullRewrite;
    // TODO decide where to validate BinaryMove and BinaryWrite instances. They are validated on cration, here and than in BinaryInPlaceWriter
    public IReadOnlyList<BinaryMove> Moves => [.. (Diff?.Moves ?? []).Where(x => !x.IsEmpty)];
    public IReadOnlyList<BinaryWrite> Writes => [.. (Diff?.Writes ?? []).Where(x => !x.IsEmpty)];

    public void WriteFullRewrite(Stream source, Stream destination) =>
      IsoBmffLayoutExecutor.ExecuteFullRewrite(source, destination, OriginalNodes, EditedNodes, Plan.Layout);
  }
}