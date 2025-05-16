using BenchmarkDotNet.Running;

namespace SourceGenerationBenchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return;
        }

        RunForProfiler(args[0]);
    }

    static void RunForProfiler(string type)
    {
        const int iterationCount = 2000;
        var b = new SerializationBenchmarks
        {
            CountDocuments = 1000
        };
        b.GlobalSetup();

        switch (type)
        {
            case "serialize_base":
                for (var i = 0; i < iterationCount; i++) b.Serialize_Binary_Base();
                break;
            case "deserialize_base":
                for (var i = 0; i < iterationCount; i++) b.Deserialize_Binary_Base();
                break;
            case "serialize_generated":
                for (var i = 0; i < iterationCount; i++) b.Serialize_Binary_Generated();
                break;
            case "deserialize_generated":
                for (var i = 0; i < iterationCount; i++) b.Deserialize_Binary_Generated();
                break;
        }
    }
}