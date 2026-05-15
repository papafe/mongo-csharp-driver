/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using BenchmarkDotNet.Running;

namespace MongoDB.Bson.SourceGeneration.Benchmarks
{
    // Entry point. Two modes:
    //   - default: hand off to BenchmarkSwitcher for the steady-state BDN suite. Usage:
    //     `dotnet run -c Release -- --filter '*'` (or any other BDN argument).
    //   - --cold-start <reflection|sourcegen>: run the cold-start harness once and print one
    //     line of timing. Drive it from a shell loop to get a distribution. See README.
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--cold-start")
            {
                var rest = new string[args.Length - 1];
                System.Array.Copy(args, 1, rest, 0, rest.Length);
                return ColdStartHarness.Run(rest);
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return 0;
        }
    }
}
