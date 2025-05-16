```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.4.1 (24E263) [Darwin 24.4.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.201
  [Host]     : .NET 8.0.5 (8.0.524.21615), Arm64 RyuJIT AdvSIMD
  Job-VCDDXH : .NET 8.0.5 (8.0.524.21615), Arm64 RyuJIT AdvSIMD

IterationCount=1  LaunchCount=15  RunStrategy=ColdStart  
WarmupCount=0  

```
| Method                       | CountDocuments | Mean      | Error     | StdDev    | Gen0      | Gen1      | Allocated   |
|----------------------------- |--------------- |----------:|----------:|----------:|----------:|----------:|------------:|
| **Deserialize_Binary_Base**      | **100**            |  **8.767 ms** | **0.6945 ms** | **0.6496 ms** |         **-** |         **-** |    **170.9 KB** |
| **Deserialize_Binary_Base**      | **1000**           | **13.334 ms** | **0.4592 ms** | **0.4295 ms** |         **-** |         **-** |  **1675.59 KB** |
| **Deserialize_Binary_Base**      | **10000**          | **65.685 ms** | **2.2671 ms** | **2.1206 ms** | **2000.0000** | **1000.0000** | **16722.48 KB** |
| **Deserialize_Binary_Generated** | **100**            |  **4.137 ms** | **0.1227 ms** | **0.1148 ms** |         **-** |         **-** |    **136.7 KB** |
| **Deserialize_Binary_Generated** | **1000**           |  **6.721 ms** | **0.3568 ms** | **0.3338 ms** |         **-** |         **-** |  **1360.14 KB** |
| **Deserialize_Binary_Generated** | **10000**          | **38.496 ms** | **1.7052 ms** | **1.5950 ms** | **2000.0000** | **1000.0000** | **13594.52 KB** |
| **Serialize_Binary_Base**        | **100**            |  **2.048 ms** | **0.1294 ms** | **0.1211 ms** |         **-** |         **-** |   **155.45 KB** |
| **Serialize_Binary_Base**        | **1000**           | **10.345 ms** | **0.5779 ms** | **0.5406 ms** |         **-** |         **-** |  **1547.64 KB** |
| **Serialize_Binary_Base**        | **10000**          | **94.909 ms** | **4.9441 ms** | **4.6247 ms** | **2000.0000** | **1000.0000** | **15469.52 KB** |
| **Serialize_Binary_Generated**   | **100**            |  **1.665 ms** | **0.0642 ms** | **0.0601 ms** |         **-** |         **-** |   **125.84 KB** |
| **Serialize_Binary_Generated**   | **1000**           |  **8.508 ms** | **0.2947 ms** | **0.2757 ms** |         **-** |         **-** |  **1257.88 KB** |
| **Serialize_Binary_Generated**   | **10000**          | **79.857 ms** | **3.1838 ms** | **2.9781 ms** | **2000.0000** | **1000.0000** | **12578.19 KB** |
