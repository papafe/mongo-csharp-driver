using BenchmarkDotNet.Running;

namespace SourceGenerationBenchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<SimpleSerializationBenchmarks>();
    }
}