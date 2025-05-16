dotnet publish -c Release -o ./out /p:DebugType=portable /p:DebugSymbols=true

mkdir -p Traces

for mode in serialize_base deserialize_base serialize_generated deserialize_generated
do
  echo "Running $mode..."
  dotnet-trace collect -o Traces/${mode} --format speedscope -- ./out/SourceGenerationBenchmarks $mode
done