using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MH.Utils;

public static class Drives {
  public static readonly Dictionary<string, string> SerialNumbers = new();

  public static void UpdateSerialNumbers() {
    SerialNumbers.Clear();

    if (OperatingSystem.IsWindows()) {
      foreach (var info in GetLogicalDrivesInfo())
        SerialNumbers.Add(info.Item1, info.Item3);
    }
    else {
      if (OperatingSystem.IsAndroid())
        SerialNumbers.Add("/storage/emulated/0", "Internal");

      foreach (var path in Environment.GetLogicalDrives().Where(x => x.StartsWith("/storage/"))) {
        var serial = Path.GetFileName(path);
        if (serial is "emulated" or "self") continue;
        SerialNumbers.Add(path, serial);
      }
    }
  }

  public static List<Tuple<string, string, string>> GetLogicalDrivesInfo() {
    var output = new List<Tuple<string, string, string>>();

    using var process = new Process();
    process.StartInfo = new("cmd") {
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      UseShellExecute = false
    };

    foreach (var drv in Environment.GetLogicalDrives()) {
      var drive = drv[..2];
      process.StartInfo.Arguments = $"/c vol {drive}";
      process.Start();
      var lines = process.StandardOutput.ReadToEnd().Split("\r\n");
      var label = lines[0].EndsWith(".") ? string.Empty : Extract(lines[0]);
      var sn = Extract(lines[1]);
      output.Add(new(drive, label, sn));
      process.WaitForExit();
    }

    return output;

    static string Extract(string s) =>
      s[(s.LastIndexOf(" ", StringComparison.OrdinalIgnoreCase) + 1)..];
  }
}
