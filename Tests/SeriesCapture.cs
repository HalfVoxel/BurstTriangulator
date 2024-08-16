using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;

namespace Benchy {
	public struct SeriesRecorder {
		public ProfilerRecorder recorder;
		public ProfilerMarker marker;
		public string name;
		public string description;
		public Aspect[] aspects;

		const int WarmupIts = 3;
		const float MaxTime = 3f;
		const int MinSamples = 5;
		const int MaxSamples = 1000;
		const int EstimateIts = MinSamples;

		public SeriesRecorder(string name, string description, Aspect[] aspects, int capacity) {
			this.name = name;
			this.description = description;
			this.aspects = aspects;
			// Avoid conflicts with other scripts
			const string UniquePrefix = "Prof";
			name = UniquePrefix + name;
			recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, name, capacity, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
			marker = new ProfilerMarker(ProfilerCategory.Scripts, name);
		}

		public void Begin() => marker.Begin();
		public void End() => marker.End();

		public override string ToString () {
			var s = name;
			if (aspects != null && aspects.Length > 0) s += " (" + string.Join(", ", aspects.Select(a => a.name + ": " + a.value).ToArray()) + ")";
			return s;
		}

		public Series ToSeries() => new Series(name, description, aspects, recorder);

		public static Series RecordSeries (string name, string description, Aspect[] aspects, System.Action action) {
			var marker = new SeriesRecorder(name, description, aspects, MaxSamples);
			Debug.Log("Starting " + marker.ToString());

			for (int i = 0; i < WarmupIts; i++) action();

			long tot = 0;
			var iterations = MaxSamples;
			for (int i = 0; i < iterations; i++) {
				marker.Begin();
				action();
				marker.End();
				if (i < EstimateIts) {
					tot += marker.recorder.LastValue;
				} else if (i == EstimateIts) {
					Assert.AreEqual(marker.recorder.UnitType, ProfilerMarkerDataUnit.TimeNanoseconds);
					var avgSeconds = tot / (EstimateIts * 1000000000.0);
					iterations = Mathf.Clamp(Mathf.RoundToInt(MaxTime / (float)avgSeconds), MinSamples, MaxSamples);
					Debug.Log("Estimating " + marker.ToString() + " to take " + (avgSeconds * iterations).ToString("0.0") + " seconds. Using " + iterations + " iterations");
				}
			}
			return marker.ToSeries();
		}
	}

	static class CmdUtils {
		public static string RunCommand (string args, string workingDirectory) {
			workingDirectory = workingDirectory.Trim();
			if (workingDirectory.StartsWith("/")) throw new System.Exception("Working directory should be relative to the project root");

			var path = Application.dataPath;
			if (Application.platform == RuntimePlatform.WindowsEditor) {
				path = path.Replace("C:/", "/mnt/c/");
			}
			args = "cd " + path + "/../" + workingDirectory + " && " + args;
			string cmdPath;

			if (Application.platform == RuntimePlatform.WindowsEditor) {
				args = "bash -c '" + args + "'";
				cmdPath = "wsl.exe";
			} else if (Application.platform == RuntimePlatform.LinuxEditor) {
				cmdPath = "bash";
				args = "-c '" + args + "'";
			} else {
				throw new System.Exception("Unsupported platform");
			}

			var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
				cmdPath, args
				) {
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
			});

			p.WaitForExit();
			if (p.ExitCode != 0) {
				Debug.Log(p.StandardOutput.ReadToEnd());
				Debug.Log(p.StandardError.ReadToEnd());
				throw new System.Exception("Failed to run " + args);
			}

			return p.StandardOutput.ReadToEnd();
		}
	}

	[System.Serializable]
	public class Result {
		public string name;
		public string version;
		public string gitHash;
		public int testVersion;
		public string recordingTime;
		public List<Series> series;

		public static Result FromEnvironment (string name, int testVersion, string gitRootDirectory = ".") {
			// Read git hash of the current commit
			var gitHash = CmdUtils.RunCommand("git rev-list --abbrev-commit --skip=0 --max-count=1 HEAD", gitRootDirectory).Trim();
			if (gitHash == "") throw new System.Exception("Failed to read git hash");
			// Find the last version tag in the git history
			var gitTag = CmdUtils.RunCommand($"git describe --abbrev=0 --match \"v*\" --tags \"{gitHash}\"", gitRootDirectory).Trim();

			var result = new Result {
				name = name,
				version = gitTag,
				gitHash = gitHash,
				testVersion = testVersion,
				recordingTime = System.DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
				series = new List<Series>()
			};
			return result;
		}

		Result[] SplitBySeries () {
			var res = new List<Result>();
			// Group by series name
			series.GroupBy(s => s.name).ToList().ForEach(g => {
				var r = new Result {
					name = name + "_" + g.Key,
					version = version,
					gitHash = gitHash,
					testVersion = testVersion,
					recordingTime = recordingTime,
					series = g.ToList()
				};
				res.Add(r);
			});
			return res.ToArray();
		}

		public void Save (string prefix) {
			foreach (var s in SplitBySeries()) {
				s.SaveInternal(prefix);
			}
		}

		void SaveInternal (string prefix) {
			var dir = Application.dataPath + $"/../Performance/{prefix}/{version}_{gitHash}/recordings";
			Debug.Log("Saved series to " + dir);
			System.IO.Directory.CreateDirectory(dir);
			var path = $"{dir}/{name}.json";
			System.IO.File.WriteAllText(path, JsonUtility.ToJson(this, true));
		}

		public void RecordSeries(string name, string description, System.Action action) => RecordSeries(name, description, null, action);

		/** Record a series of measurements.
		 *
		 * The action is called a number of times to estimate the time it takes to run.
		 * The number of iterations is adjusted to take about #SeriesRecorder.MaxTime seconds.
		 *
		 * The aspects represent minor variations of the series. Multiple series may have the same name, but different aspects.
		 * This can be used to group similar series together when plotting.
		 */
		public void RecordSeries (string name, string description, Aspect[] aspects, System.Action action) {
			series.Add(SeriesRecorder.RecordSeries(name, description, aspects, action));
		}
	}

	[System.Serializable]
	public struct Aspect {
		public string name;
		public string value;

		public Aspect (string name, float value) {
			this.name = name;
			this.value = value.ToString("0.000");
		}

		public Aspect (string name, string value) {
			this.name = name;
			this.value = value;
		}

		public Aspect (string name, int value) {
			this.name = name;
			this.value = value.ToString();
		}
	}

	[System.Serializable]
	public struct Series {
		public string name;
		public string description;
		public string unit;
		public long[] samples;
		public Aspect[] aspects;

		public Series(string name, string description, Aspect[] aspects, string unit, long[] samples) {
			this.name = name;
			this.description = description;
			this.aspects = aspects;
			this.unit = unit;
			this.samples = samples;
		}

		public Series(string name, string description, Aspect[] aspects, ProfilerRecorder recorder) {
			this.name = name;
			this.description = description;
			this.aspects = aspects;
			unit = recorder.UnitType.ToString();
			var samples = new List<ProfilerRecorderSample>(recorder.Count);
			recorder.CopyTo(samples);
			this.samples = samples.Select(s => s.Value).ToArray();
			for (int i = 0; i < samples.Count; i++) {
				if (samples[i].Count != 1) throw new System.Exception("Count should be 1");
			}
		}
	}
}