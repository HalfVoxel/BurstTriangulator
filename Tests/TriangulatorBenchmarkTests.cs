using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace andywiecko.BurstTriangulator.Editor.Tests
{
    [BurstCompile]
    static class BurstCompileAssert {
        [BurstCompile]
        public static void AssertIsBurst () {
            ThrowIfNotBurst();
        }

        [BurstDiscard]
        static void ThrowIfNotBurst () {
            throw new System.Exception("Burst is not enabled");
        }
    }

    [Explicit, Category("Benchmark")]
    public class TriangulatorBenchmarkTests
    {
        bool debuggerInitialValue;

        [OneTimeSetUp]
        public void Setup () {
            UnityEngine.Debug.Log("Setting up Burst...");
            // Disable compiler while setting options, to avoid triggering recompilations for every change
            BurstCompiler.Options.EnableBurstCompilation = false;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
            BurstCompiler.Options.EnableBurstDebug = false;
            BurstCompiler.Options.ForceEnableBurstSafetyChecks = false;
            BurstCompiler.Options.EnableBurstSafetyChecks = false;

            // Re-enable compiler. This will force a recompilation
            BurstCompiler.Options.EnableBurstCompilation = true;

            if (!BurstCompiler.Options.IsEnabled) {
                throw new System.Exception("Burst must be enabled for these tests");
            }

            debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            BurstCompileAssert.AssertIsBurst();
        }

        [OneTimeTearDown]
        public void TearDown () {
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        static TestCaseData DelaunayCase(int count, int N) => new((count: count, N: N))
        {
            TestName = $"Points: {count * count}"
        };
        private static readonly TestCaseData[] delaunayBenchmarkTestData =
        {
            DelaunayCase(count: 10, N: 1000),
            DelaunayCase(count: 20, N: 1000),
            DelaunayCase(count: 31, N: 1000),
            DelaunayCase(count: 50, N: 100),
            DelaunayCase(count: 100, N: 100),
            DelaunayCase(count: 200, N: 100),
            DelaunayCase(count: 300, N: 100),
            DelaunayCase(count: 400, N: 100),
            DelaunayCase(count: 500, N: 100),
            DelaunayCase(count: 600, N: 10),
            DelaunayCase(count: 700, N: 10),
            DelaunayCase(count: 800, N: 10),
            DelaunayCase(count: 900, N: 10),
            DelaunayCase(count: 1000, N: 10),
        };

        [Test, TestCaseSource(nameof(delaunayBenchmarkTestData))]
        public void DelaunayBenchmarkFloat2Test((int count, int N) input)
        {
            var (count, N) = input;
            var debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            var points = new List<float2>(count * count);
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var p = math.float2(i / (float)(count - 1), j / (float)(count - 1));
                    points.Add(p);
                }
            }

            using var positions = new NativeArray<float2>(points.ToArray(), Allocator.Persistent);

            var stopwatch = Stopwatch.StartNew();
            using var triangulator = new Triangulator<float2>(capacity: count * count, Allocator.Persistent)
            {
                Input = { Positions = positions },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false,
                },
            };

            var dependencies = default(JobHandle);
            for (int i = 0; i < N; i++) dependencies = triangulator.Schedule(dependencies);
            dependencies.Complete();
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{count * count} {stopwatch.Elapsed.TotalMilliseconds / N}");

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        private static readonly TestCaseData[] delaunayBenchmarkDouble2TestData = delaunayBenchmarkTestData
            .Select(i => new TestCaseData(i.Arguments) { TestName = i.TestName + " (double2)" })
            .ToArray();

        [Test, TestCaseSource(nameof(delaunayBenchmarkDouble2TestData))]
        public void DelaunayBenchmarkDouble2Test((int count, int N) input)
        {
            var (count, N) = input;
            var debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            var points = new List<double2>(count * count);
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var p = math.float2(i / (float)(count - 1), j / (float)(count - 1));
                    points.Add(p);
                }
            }

            using var positions = new NativeArray<double2>(points.ToArray(), Allocator.Persistent);

            var stopwatch = Stopwatch.StartNew();
            using var triangulator = new Triangulator<double2>(capacity: count * count, Allocator.Persistent)
            {
                Input = { Positions = positions },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false,
                },
            };

            var dependencies = default(JobHandle);
            for (int i = 0; i < N; i++) dependencies = triangulator.Schedule(dependencies);
            dependencies.Complete();
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{count * count} {stopwatch.Elapsed.TotalMilliseconds / N}");

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        private static readonly TestCaseData[] delaunayBenchmarkInt2TestData = delaunayBenchmarkTestData
            .Select(i => new TestCaseData(i.Arguments) { TestName = i.TestName + " (int2)" })
            .ToArray();

        [Test, TestCaseSource(nameof(delaunayBenchmarkInt2TestData))]
        public void DelaunayBenchmarkInt2Test((int count, int N) input)
        {
            var (count, N) = input;
            var debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            var points = new List<int2>(count * count);
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var p = math.int2(i, j);
                    points.Add(p);
                }
            }

            using var positions = new NativeArray<int2>(points.ToArray(), Allocator.Persistent);

            var stopwatch = Stopwatch.StartNew();
            using var triangulator = new Triangulator<int2>(capacity: count * count, Allocator.Persistent)
            {
                Input = { Positions = positions },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false,
                },
            };

            var dependencies = default(JobHandle);
            for (int i = 0; i < N; i++) dependencies = triangulator.Schedule(dependencies);
            dependencies.Complete();
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{count * count} {stopwatch.Elapsed.TotalMilliseconds / N}");

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        private static readonly TestCaseData[] constraintBenchmarkTestData = Enumerable
            .Range(0, 80)
            .Select(i => new TestCaseData((100, 3 * (i + 1))))
            .ToArray();

        [Test, TestCaseSource(nameof(constraintBenchmarkTestData))]
        public void ConstraintBenchmarkTest((int count, int N) input)
        {
            var (count, N) = input;
            var debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            var points = new List<float2>(count * count);
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    var p = math.float2(i / (float)(count - 1), j / (float)(count - 1));
                    points.Add(p);
                }
            }

            var offset = points.Count;
            var constraints = new List<int>(N + 1);
            for (int i = 0; i < N; i++)
            {
                var phi = 2 * math.PI / N * i + 0.1452f;
                var p = 0.2f * math.float2(math.cos(phi), math.sin(phi)) + 0.5f;
                points.Add(p);
                constraints.Add(offset + i);
                constraints.Add(offset + (i + 1) % N);
            }

            using var positions = new NativeArray<float2>(points.ToArray(), Allocator.Persistent);
            using var constraintEdges = new NativeArray<int>(constraints.ToArray(), Allocator.Persistent);

            var stopwatch = Stopwatch.StartNew();
            using var triangulator = new Triangulator<float2>(capacity: count * count + N, Allocator.Persistent)
            {
                Input = { Positions = positions, ConstraintEdges = constraintEdges },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false
                },
            };

            var dependencies = default(JobHandle);
            var rep = 300;
            for (int i = 0; i < rep; i++) dependencies = triangulator.Schedule(dependencies);
            dependencies.Complete();
            stopwatch.Stop();
            var log = $"{N} {stopwatch.Elapsed.TotalMilliseconds / rep}";
            UnityEngine.Debug.Log(log);

            // NOTE: Uncomment this to write all the test cases into a file.
            //using var writer = new System.IO.StreamWriter("tmp.txt", true);
            //writer.WriteLine(log);

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        private static readonly TestCaseData[] refineMeshBenchmarkTestData =
        {
            new((area: 10.000f, N: 100)),
            new((area: 05.000f, N: 100)),
            new((area: 01.000f, N: 100)),
            new((area: 0.5000f, N: 100)),
            new((area: 0.1000f, N: 100)),
            new((area: 0.0500f, N: 100)),
            new((area: 0.0100f, N: 100)),
            new((area: 0.0050f, N: 100)),
            new((area: 0.0030f, N: 100)),
            new((area: 0.0010f, N: 100)),
            new((area: 0.0007f, N: 010)),
            new((area: 0.0005f, N: 010)),
            new((area: 0.0004f, N: 010)),
            new((area: 0.0003f, N: 010)),
            new((area: 0.0002f, N: 005)),
        };

        [Test, TestCaseSource(nameof(refineMeshBenchmarkTestData))]
        public void RefineMeshBenchmarkTest((float area, int N) input)
        {
            var (area, N) = input;
            var debuggerInitialValue = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled;
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;

            var stopwatch = Stopwatch.StartNew();
            using var points = new NativeArray<float2>(new[]
            {
                math.float2(-1, -1),
                math.float2(+1, -1),
                math.float2(+1, +1),
                math.float2(-1, +1),
            }, Allocator.Persistent);
            using var triangulator = new Triangulator<float2>(capacity: 64 * 1024, Allocator.Persistent)
            {
                Input = { Positions = points },
                Settings = {
                    RefineMesh = true,
                    RestoreBoundary = false,
                    ValidateInput = false,
                    RefinementThresholds = { Area = area },
                },
            };

            var dependencies = default(JobHandle);
            for (int i = 0; i < N; i++) dependencies = triangulator.Schedule(dependencies);
            dependencies.Complete();
            stopwatch.Stop();
            UnityEngine.Debug.Log($"{triangulator.Output.Triangles.Length} {stopwatch.Elapsed.TotalMilliseconds / N}");

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }
    }
}