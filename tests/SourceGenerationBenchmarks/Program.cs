using BenchmarkDotNet.Running;

namespace SourceGenerationBenchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // var b = new SerializationBenchmarksAOT();
        // b.CountDocuments = 10;
        // b.GlobalSetup();
        // b.Deserialize_Binary_Generated();

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}