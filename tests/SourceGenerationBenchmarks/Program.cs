using BenchmarkDotNet.Running;

namespace SourceGenerationBenchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // var b = new ComplexSerializationBenchmarksColdStart();
        // b.GlobalSetupGenerated();
        // b.Serialize_Generated();


        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}