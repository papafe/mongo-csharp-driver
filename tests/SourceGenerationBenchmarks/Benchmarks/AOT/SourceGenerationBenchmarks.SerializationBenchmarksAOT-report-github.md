```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.4.1 (24E263) [Darwin 24.4.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 8.0.5 (8.0.524.21615), Arm64 RyuJIT AdvSIMD
  Job-JWWMQU : .NET 8.0.14, Arm64 NativeAOT AdvSIMD

Runtime=NativeAOT 8.0  Toolchain=Latest ILCompiler  

```
| Method                       | CountDocuments | Mean         | Error      | StdDev     | Gen0      | Gen1     | Gen2     | Allocated   |
|----------------------------- |--------------- |-------------:|-----------:|-----------:|----------:|---------:|---------:|------------:|
| **Deserialize_Binary_Generated** | **100**            |     **98.29 μs** |   **1.021 μs** |   **0.955 μs** |   **22.0947** |   **2.1973** |        **-** |   **135.98 KB** |
| **Deserialize_Binary_Generated** | **1000**           |  **1,024.62 μs** |  **12.580 μs** |  **11.768 μs** |  **220.7031** |  **82.0313** |        **-** |  **1359.42 KB** |
| **Deserialize_Binary_Generated** | **10000**          | **15,141.19 μs** |  **17.256 μs** |  **13.473 μs** | **2437.5000** | **859.3750** | **218.7500** | **13593.88 KB** |
| **Serialize_Binary_Generated**   | **100**            |    **105.30 μs** |   **0.673 μs** |   **0.597 μs** |   **20.3857** |   **1.3428** |        **-** |   **125.13 KB** |
| **Serialize_Binary_Generated**   | **1000**           |  **1,063.93 μs** |  **11.253 μs** |   **9.975 μs** |  **205.0781** |  **62.5000** |        **-** |  **1257.16 KB** |
| **Serialize_Binary_Generated**   | **10000**          | **11,768.85 μs** | **227.116 μs** | **212.444 μs** | **2171.8750** | **656.2500** | **125.0000** | **12577.52 KB** |
