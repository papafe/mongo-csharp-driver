```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.4.1 (24E263) [Darwin 24.4.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 8.0.5 (8.0.524.21615), Arm64 RyuJIT AdvSIMD
  Job-GODMEM : .NET 8.0.14, Arm64 NativeAOT AdvSIMD

Runtime=NativeAOT 8.0  IterationCount=1  LaunchCount=15  
RunStrategy=ColdStart  WarmupCount=0  

```
| Method                       | CountDocuments | Mean        | Error     | StdDev    | Gen0      | Gen1      | Allocated   |
|----------------------------- |--------------- |------------:|----------:|----------:|----------:|----------:|------------:|
| **Deserialize_Binary_Generated** | **100**            |    **113.7 μs** |   **7.47 μs** |   **6.99 μs** |         **-** |         **-** |   **136.36 KB** |
| **Deserialize_Binary_Generated** | **1000**           |    **982.9 μs** |  **45.34 μs** |  **42.42 μs** |         **-** |         **-** |   **1359.8 KB** |
| **Deserialize_Binary_Generated** | **10000**          | **14,810.5 μs** | **168.36 μs** | **157.49 μs** | **2000.0000** | **1000.0000** | **13594.17 KB** |
| **Serialize_Binary_Generated**   | **100**            |    **114.3 μs** |   **4.09 μs** |   **3.83 μs** |         **-** |         **-** |    **125.5 KB** |
| **Serialize_Binary_Generated**   | **1000**           |  **1,084.9 μs** |  **25.02 μs** |  **23.40 μs** |         **-** |         **-** |  **1257.53 KB** |
| **Serialize_Binary_Generated**   | **10000**          | **13,226.2 μs** | **114.39 μs** | **107.00 μs** | **2000.0000** | **1000.0000** | **12577.84 KB** |
