using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Benchy;
using Unity.Burst;
using andywiecko.BurstTriangulator.LowLevel.Unsafe;

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

    [BurstCompile]
    struct UnsafeTriangulateJob: IJob {
        public UnsafeTriangulator<float2> unsafeTriangulator;
        public LowLevel.Unsafe.InputData<float2> input;
        public LowLevel.Unsafe.OutputData<float2> output;
        public Args args;
        public int repetitions;

        public void Execute() {
            for (int i = 0; i < repetitions; i++) {
                LowLevel.Unsafe.Extensions.Triangulate(new UnsafeTriangulator<float2>(), input, output, args, Allocator.Temp);
            }
        }
    }

    [Explicit, Category("Benchmark")]
    public class TriangulatorBenchmarkTests
    {
        Result result;
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

            result = Result.FromEnvironment("Triangulation", 1, "Packages/com.andywiecko.burst.triangulator");

            // Used when locally testing changes without committing them
            // result.version += ".inline_markers";
        }

        [OneTimeTearDown]
        public void TearDown () {
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
            result.Save("triangulator");
            UnityEngine.Debug.Log(result.GenerateSummary());
        }

        static TestCaseData DelaunayCase(int count) => new(count)
        {
            TestName = $"Points: {count * count}"
        };
        private static readonly TestCaseData[] delaunayBenchmarkTestData =
        {
            DelaunayCase(count: 3),
            DelaunayCase(count: 10),
            DelaunayCase(count: 20),
            DelaunayCase(count: 31),
            DelaunayCase(count: 50),
            DelaunayCase(count: 100),
            DelaunayCase(count: 200),
            DelaunayCase(count: 300),
            DelaunayCase(count: 400),
            DelaunayCase(count: 500),
            DelaunayCase(count: 600),
            DelaunayCase(count: 700),
            DelaunayCase(count: 800),
            DelaunayCase(count: 900),
            DelaunayCase(count: 1000),
        };

        [Test, TestCaseSource(nameof(delaunayBenchmarkTestData))]
        public void DelaunayBenchmarkFloat2Test(int count)
        {
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

            result.RecordSeries("Delaunay F32", "Only delaunay triangulation", new Aspect[] { new Aspect("Edges", count) }, () => {
                triangulator.Schedule().Complete();
            });
        }

        static TestCaseData RepeatedDelaunayCase(int count, int repetitions, string suffix) => new((count: count, repetitions: repetitions))
        {
            TestName = $"Points: {count * count}{suffix}"
        };
        private static readonly TestCaseData[] delaunayBenchmarkFloat2UnsafeTestData =
        {
            RepeatedDelaunayCase(count: 3, repetitions: 100, " (unsafe)"),
            RepeatedDelaunayCase(count: 10, repetitions: 100, " (unsafe)"),
            RepeatedDelaunayCase(count: 20, repetitions: 100, " (unsafe)"),
            RepeatedDelaunayCase(count: 31, repetitions: 100, " (unsafe)"),
            RepeatedDelaunayCase(count: 50, repetitions: 10, " (unsafe)"),
            RepeatedDelaunayCase(count: 100, repetitions: 10, " (unsafe)"),
            RepeatedDelaunayCase(count: 200, repetitions: 10, " (unsafe)"),
            RepeatedDelaunayCase(count: 300, repetitions: 10, " (unsafe)"),
            RepeatedDelaunayCase(count: 400, repetitions: 10, " (unsafe)"),
            RepeatedDelaunayCase(count: 500, repetitions: 10, " (unsafe)"),
        };

        [Test, TestCaseSource(nameof(delaunayBenchmarkFloat2UnsafeTestData))]
        public void DelaunayBenchmarkFloat2UnsafeTest((int count, int repetitions) input)
        {
            var (count, repetitions) = input;

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

            var triangulator = new UnsafeTriangulator<float2>();
            var inputData = new LowLevel.Unsafe.InputData<float2> {
                Positions = positions
            };
            var output = new LowLevel.Unsafe.OutputData<float2> {
                Triangles = new NativeList<int>(0, Allocator.TempJob)
            };
            var args = Args.Default().With(
            refineMesh:  false,
            restoreBoundary:  false,
            validateInput:  false);

            result.RecordSeries("Delaunay F32 (unsafe)", "Only delaunay triangulation. Repeats multiple times in a single job.", new Aspect[] { new Aspect("Edges", count), new Aspect("Reps", repetitions) }, () => {
                new UnsafeTriangulateJob {
                    unsafeTriangulator = triangulator,
                    input = inputData,
                    output = output,
                    args = args,
                    repetitions = repetitions,
                }.Run();
            });

            output.Triangles.Dispose();
        }

        private static readonly TestCaseData[] delaunayBenchmarkDouble2TestData = delaunayBenchmarkTestData
            .Select(i => new TestCaseData(i.Arguments) { TestName = i.TestName + " (double2)" })
            .ToArray();

        [Test, TestCaseSource(nameof(delaunayBenchmarkDouble2TestData))]
        public void DelaunayBenchmarkDouble2Test(int count)
        {
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

            using var triangulator = new Triangulator<double2>(capacity: count * count, Allocator.Persistent)
            {
                Input = { Positions = positions },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false,
                },
            };

            result.RecordSeries("Delaunay F64", "Only delaunay triangulation", new Aspect[] { new Aspect("Edges", count) }, () => {
                triangulator.Schedule().Complete();
            });

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = debuggerInitialValue;
        }

        private static readonly TestCaseData[] delaunayBenchmarkInt2TestData = delaunayBenchmarkTestData
            .Select(i => new TestCaseData(i.Arguments) { TestName = i.TestName + " (int2)" })
            .ToArray();

        [Test, TestCaseSource(nameof(delaunayBenchmarkInt2TestData))]
        public void DelaunayBenchmarkInt2Test(int count)
        {
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

            using var triangulator = new Triangulator<int2>(capacity: count * count, Allocator.Persistent)
            {
                Input = { Positions = positions },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false,
                },
            };

            result.RecordSeries("Delaunay I32", "Only delaunay triangulation", new Aspect[] { new Aspect("Edges", count) }, () => {
                triangulator.Schedule().Complete();
            });
        }

        private static readonly TestCaseData[] constraintBenchmarkTestData = Enumerable
            .Range(0, 8)
            .Select(i => new TestCaseData((100, 3 * 10 * (i + 1))))
            .ToArray();

        InputData<float2> ConstraintInput(int count, int N) {
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

            var positions = new NativeArray<float2>(points.ToArray(), Allocator.Persistent);
            var constraintEdges = new NativeArray<int>(constraints.ToArray(), Allocator.Persistent);
            return new InputData<float2> { Positions = positions, ConstraintEdges = constraintEdges };
        }

        [Test, TestCaseSource(nameof(constraintBenchmarkTestData))]
        public void ConstrainedTriangulationBenchmarkFloat2Test((int count, int N) input)
        {
            var (count, N) = input;
            var inputData = ConstraintInput(count, N);

            using var triangulator = new Triangulator<float2>(capacity: count * count + N, Allocator.Persistent)
            {
                Input = inputData,
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = false
                },
            };

            result.RecordSeries("Constrained F32", "Triangulation", new Aspect[] { new Aspect("ConstrainedEdges", N) }, () => {
                triangulator.Schedule().Complete();
            });

            inputData.Positions.Dispose();
            inputData.ConstraintEdges.Dispose();
        }

        static NativeArray<int2> ToFixedPrecision(NativeArray<float2> points, float scale, Allocator allocator)
        {
            var result = new NativeArray<int2>(points.Length, allocator);
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = (int2)(math.round(points[i] * scale));
            }
            return result;
        }

        [Test, TestCaseSource(nameof(constraintBenchmarkTestData))]
        public void ConstrainedTriangulationBenchmarkInt2Test((int count, int N) input)
        {
            var (count, N) = input;
            var inputDataFloat = ConstraintInput(count, N);

            using var positions = ToFixedPrecision(inputDataFloat.Positions, 10000, Allocator.Persistent);

            var triangulator = new Triangulator<int2>(0, Allocator.Temp) {
                Input = new InputData<int2> {
                    Positions = positions,
                    ConstraintEdges = inputDataFloat.ConstraintEdges
                },
                Settings = {
                    RefineMesh = false,
                    RestoreBoundary = false,
                    ValidateInput = true
                }
            };

            result.RecordSeries("Constrained I32", "Triangulation", new Aspect[] { new Aspect("ConstrainedEdges", N) }, () => {
                triangulator.Run();
            });

            inputDataFloat.Positions.Dispose();
            inputDataFloat.ConstraintEdges.Dispose();
            triangulator.Dispose();
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

            result.RecordSeries("RefineMesh F32", "Triangulate and refine mesh", new Aspect[] { new Aspect("Area", area) }, () => {
                triangulator.Schedule().Complete();
            });
        }

        [Test, TestCaseSource(nameof(refineMeshBenchmarkTestData))]
        public void RefineMeshBenchmarkTestF64((float area, int N) input)
        {
            var (area, N) = input;

            using var points = new NativeArray<double2>(new[]
            {
                math.double2(-1, -1),
                math.double2(+1, -1),
                math.double2(+1, +1),
                math.double2(-1, +1),
            }, Allocator.Persistent);
            using var triangulator = new Triangulator<double2>(capacity: 64 * 1024, Allocator.Persistent)
            {
                Input = { Positions = points },
                Settings = {
                    RefineMesh = true,
                    RestoreBoundary = false,
                    ValidateInput = false,
                    RefinementThresholds = { Area = area },
                },
            };

            result.RecordSeries("RefineMesh F64", "Triangulate and refine mesh", new Aspect[] { new Aspect("Area", area) }, () => {
                triangulator.Schedule().Complete();
            });
        }
    }
}