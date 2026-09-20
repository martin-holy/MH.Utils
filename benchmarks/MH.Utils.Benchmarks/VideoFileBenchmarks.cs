using BenchmarkDotNet.Attributes;
using MH.Utils.Imaging;
using MH.Utils.Imaging.IsoBmff;

namespace MH.Utils.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class VideoFileBenchmarks {
  private FileStream _stream = null!;

  [GlobalSetup]
  public void Setup() {
    _stream = File.OpenRead("e:\\Pictures\\01 Digital_Foto\\-=Sklad\\vid\\new\\20240929_162412.mp4");
  }

  /*| Method         | Mean        | Error      | StdDev    | Gen0   | Allocated |
    |--------------- |------------:|-----------:|----------:|-------:|----------:|
    | ReadAll        | 3,064.92 ns | 387.951 ns | 21.265 ns | 0.0648 |     136 B |
    | CtorOnly       |    13.46 ns |   0.308 ns |  0.017 ns | 0.0344 |      72 B |
    | FindMoov       |   112.68 ns |  14.889 ns |  0.816 ns | 0.0343 |      72 B |
    | FindVideoTrack |   561.83 ns | 570.806 ns | 31.288 ns | 0.0343 |      72 B |
    | FindTkhd       |   641.41 ns | 553.502 ns | 30.339 ns | 0.0343 |      72 B |*/
  [Benchmark]
  public void ReadAll() {
    var vid = new VideoMetadata(_stream);
  }

  [Benchmark]
  public void CtorOnly() {
    var reader = new IsoBmffReader(_stream);
  }

  [Benchmark]
  public void FindMoov() {
    var reader = new IsoBmffReader(_stream);
    var moov = reader.FindMoov() ?? throw new InvalidDataException("ISO BMFF moov box not found.");
  }

  [Benchmark]
  public void FindVideoTrack() {
    var reader = new IsoBmffReader(_stream);
    var moov = reader.FindMoov() ?? throw new InvalidDataException("ISO BMFF moov box not found.");
    var trak = reader.FindVideoTrack(moov) ?? throw new InvalidDataException("Video track not found.");
  }

  [Benchmark]
  public void FindTkhd() {
    var reader = new IsoBmffReader(_stream);
    var moov = reader.FindMoov() ?? throw new InvalidDataException("ISO BMFF moov box not found.");
    var trak = reader.FindVideoTrack(moov) ?? throw new InvalidDataException("Video track not found.");
    var tkhd = reader.FindChild(trak, IsoBmffTypes.Tkhd) ?? throw new InvalidDataException("Video track has no tkhd box.");
  }
}