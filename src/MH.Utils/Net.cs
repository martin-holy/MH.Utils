using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MH.Utils;

public static class Net {
  public static Task DownloadAndSaveFile(string url, string filePath) =>
    DownloadAndSaveFile(url, filePath, CancellationToken.None);

  public static async Task DownloadAndSaveFile(string url, string filePath, CancellationToken token) {
    using var client = new HttpClient();
    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
    await stream.CopyToAsync(fileStream, 81920, token).ConfigureAwait(false);
  }

  public static Task<string?> GetWebPageContent(string url, string language = "en") =>
    GetWebPageContent(url, CancellationToken.None, language);

  public static async Task<string?> GetWebPageContent(string url, CancellationToken token, string language = "en") {
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
    client.DefaultRequestHeaders.Add("Accept-Language", language);

    try {
      var response = await client.GetAsync(url, token).ConfigureAwait(false);

      if (!response.Content.Headers.ContentEncoding.Contains("gzip"))
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

      var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
      return await DecompressContent(stream).ConfigureAwait(false);

    }
    catch (OperationCanceledException) {
      return null;
    }
    catch (Exception ex) {
      Log.Error(ex);
      return null;
    }
  }

  private static async Task<string> DecompressContent(Stream content) {
    await using var gzip = new GZipStream(content, CompressionMode.Decompress);
    using var reader = new StreamReader(gzip);
    return await reader.ReadToEndAsync().ConfigureAwait(false);
  }
}