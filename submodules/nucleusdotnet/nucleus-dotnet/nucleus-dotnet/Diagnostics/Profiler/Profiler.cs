using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Nucleus.Diagnostics {
    /// <summary>
    /// Simple profiler to measure task timing by unique-key
    /// </summary>
    public static class Profiler {
        private static readonly Dictionary<MeasureKey, MeasureData> measuring;

        public static List<MeasureData> AllData { get; private set; }

        static Profiler() {
            measuring = new Dictionary<MeasureKey, MeasureData>();
            AllData = new List<MeasureData>();
        }

        /// <summary>
        /// Returns currently evealuating Profiler data by the ThreadID
        /// </summary>
        /// <param name="threadId"></param>
        /// <returns></returns>
        public static MeasureData? GetCurrentlyMeasuringByThread(int threadId) {
            lock (measuring) {
                MeasureData data = measuring.Values.FirstOrDefault(c => c.ThreadID == threadId);
                if (data.Equals(default(MeasureData))) {
                    return null;
                }
                return data;
            }
        }

        public static void StartMeasuring(object parent, string uniqueKey, int thread) {
            MeasureKey reference = new MeasureKey(parent, uniqueKey);
            lock (measuring) {
                if (!measuring.ContainsKey(reference)) {
                    measuring.Add(reference, new MeasureData(reference, Stopwatch.GetTimestamp(), thread));
                }
            }
        }

        public static double EndMeasuring(object parent, string uniqueKey) {
            MeasureKey reference = new MeasureKey(parent, uniqueKey);
            MeasureData timestamp = measuring[reference];
            lock (measuring) {
                measuring.Remove(reference);
            }

            long now = Stopwatch.GetTimestamp();
            double result = (now - timestamp.RecordedValue) / Stopwatch.Frequency;
            AllData.Add(new MeasureData(reference, result, timestamp.ThreadID));

            return result;
        }
    }
}
