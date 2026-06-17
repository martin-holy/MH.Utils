using MH.Utils.BaseClasses;
using MH.Utils.DB.DataSources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MH.Utils.DB;

public class SimpleDB : ObservableObject {
  private readonly List<ICsvRepositoryDataSource> _repositories = [];
  private readonly List<IRelationDataSource> _relations = [];
  private readonly string _isSequencesFilePath;
  private int _changes;
  private bool _needBackUp;

  public string DbDir { get; }
  public int Changes { get => _changes; set { _changes = value; Tasks.Dispatch(() => OnPropertyChanged(nameof(Changes))); } }
  public Dictionary<string, int> IdSequences { get; } = [];
  public bool IsReady { get; private set; }

  public event EventHandler ReadyEvent = delegate { };

  public SimpleDB(string dbDir) {
    DbDir = dbDir;
    Directory.CreateDirectory(DbDir);
    _isSequencesFilePath = Path.Combine(DbDir, "IdSequences.csv");
    _loadIdSequences();
  }

  public void SetIsReady() {
    IsReady = true;
    _raiseReadyEvent();
  }

  private void _raiseReadyEvent() => ReadyEvent(this, EventArgs.Empty);

  public void AddRepositoryDataSource(ICsvRepositoryDataSource dataSource) {
    if (!IdSequences.TryGetValue(dataSource.Name, out var maxId))
      IdSequences.Add(dataSource.Name, 0);

    dataSource.Repository.MaxId = maxId;
    _repositories.Add(dataSource);
  }

  public void AddRelationDataSource(IRelationDataSource rds) {
    _relations.Add(rds);
  }

  public void FillRepositories() {
    foreach (var ds in _repositories)
      ds.FillRepository();
  }

  public void ClearDataSources() {
    foreach (var ds in _repositories)
      ds.Clear();
  }

  public void LoadAll(IProgress<string> progress) {
    foreach (var ds in _repositories) {
      progress?.Report($"Loading data for {ds.Name}");
      ds.Load();
      ds.LoadProps();
    }

    foreach (var rds in _relations) {
      progress?.Report($"Loading data for {rds.Name}");
      rds.Load();
      rds.LoadProps();
    }
  }

  public void LinkReferences(IProgress<string> progress) {
    foreach (var ds in _repositories) {
      progress?.Report($"Loading data for {ds.Name}");
      try {
        ds.LinkReferences();
      }
      catch (Exception ex) {
        Log.Error(ex, ds.Name);
      }
    }
  }

  public void SaveAll() {
    foreach (var ds in _repositories) {
      if (ds.Repository.IsModified)
        if (ds.Save())
          ds.Repository.IsModified = false;

      if (ds.Repository.ArePropsModified)
        if (ds.SaveProps())
          ds.Repository.ArePropsModified = false;
    }

    foreach (var ds in _relations) {
      if (ds.Relation.IsModified)
        if (ds.Save())
          ds.Relation.IsModified = false;

      if (ds.Relation.ArePropsModified)
        if (ds.SaveProps())
          ds.Relation.ArePropsModified = false;
    }

    SaveIdSequences();
    Changes = 0;
  }

  private void _loadIdSequences() {
    LoadFromFile(
      line => {
        var vals = line.Split('|');
        if (vals.Length != 2)
          throw new ArgumentException("Incorrect number of values.", line);
        IdSequences.Add(vals[0], int.Parse(vals[1]));
      },
      _isSequencesFilePath);
  }

  public void SaveIdSequences() {
    // check if something changed
    var isModified = false;
    foreach (var ds in _repositories) {
      if (IdSequences[ds.Name] == ds.Repository.MaxId) continue;
      IdSequences[ds.Name] = ds.Repository.MaxId;
      isModified = true;
    }

    if (!isModified) return;

    SaveToFile(
      _repositories,
      x => string.Join("|", x.Name, x.Repository.MaxId),
      _isSequencesFilePath);
  }

  public void AddChange() {
    Changes++;
    _needBackUp = true;
  }

  public void BackUp() {
    if (!_needBackUp) return;

    try {
      using var zip = ZipFile.Open(Path.Combine(DbDir, DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip"), ZipArchiveMode.Create);
      var schemaFilePath = Path.Combine(DbDir, "SchemaVersion");
      if (File.Exists(schemaFilePath))
        zip.CreateEntryFromFile(schemaFilePath, schemaFilePath);

      foreach (var file in Directory.EnumerateFiles(DbDir, "*.csv"))
        _ = zip.CreateEntryFromFile(file, file);
    }
    catch (Exception ex) {
      Log.Error(ex, "Error while backing up database.");
    }
  }

  public static bool LoadFromFile(Action<string> parseLine, string filePath) {
    if (!File.Exists(filePath)) return false;
    try {
      using var sr = new StreamReader(filePath, Encoding.UTF8);
      while (sr.ReadLine() is { } line)
        parseLine(line);

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex);
      return false;
    }
  }

  public static bool SaveToFile<T>(IEnumerable<T> items, Func<T, string> toString, string filePath) {
    try {
      using var sw = new StreamWriter(filePath, false, Encoding.UTF8, 65536);
      foreach (var item in items)
        sw.WriteLine(toString(item));

      return true;
    }
    catch (Exception ex) {
      Log.Error(ex);
      return false;
    }
  }

  public void Migrate(int newVersion, Action<int, int> migrationResolver) {
    try {
      var vFilePath = Path.Combine(DbDir, "SchemaVersion");
      var oldVersion = File.Exists(vFilePath)
        ? int.Parse(File.ReadAllLines(vFilePath, Encoding.UTF8)[0])
        : newVersion;

      if (oldVersion == newVersion) return;
      migrationResolver(oldVersion, newVersion);
      File.WriteAllText(vFilePath, newVersion.ToString(), Encoding.UTF8);
    }
    catch (Exception ex) {
      Log.Error(ex);
    }
  }

  public static void MigrateFile(string filePath, Func<string, string> migrateRecord) {
    if (!File.Exists(filePath)) return;

    var newFilePath = filePath + "_tmpFile";
    using var sr = new StreamReader(filePath, Encoding.UTF8);
    using var sw = new StreamWriter(newFilePath, false, Encoding.UTF8, 65536);

    while (sr.ReadLine() is { } line)
      sw.WriteLine(migrateRecord(line));

    sr.Close();
    sw.Close();
    File.Move(newFilePath, filePath, true);
  }

  public static int? GetNextRecycledId(HashSet<int> usedIds) {
    if (usedIds.Count == 0) return null;

    var id = 0;
    var max = usedIds.Max();

    for (var i = 1; i < max + 1; i++)
      if (!usedIds.Contains(i)) {
        id = i;
        break;
      }

    if (id == 0) return null;
    return id;
  }

  public string GetDBFilePath(string fileName) =>
    Path.Combine(DbDir, $"{fileName}.csv");

  public string GetDBFilePath(string drive, string fileName) {
    var oldPath = string.Join(Path.DirectorySeparatorChar, DbDir, $"{fileName}.{drive[..1]}.csv");
    var newPath = string.Join(Path.DirectorySeparatorChar, DbDir, $"{fileName}.{Drives.SerialNumbers[drive]}.csv");

    if (File.Exists(oldPath))
      File.Move(oldPath, newPath);

    return newPath;
  }
}