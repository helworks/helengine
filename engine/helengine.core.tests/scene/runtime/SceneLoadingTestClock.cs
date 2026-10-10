using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Advances one deterministic millisecond per observation so tests can control cooperative step counts.</summary>
    internal sealed class SceneLoadingTestClock : IRuntimeSceneLoadingClock {
        /// <summary>Stores the next monotonically increasing observation.</summary>
        double Next;
        /// <summary>Stores optional exact observations for invalid-clock regression cases.</summary>
        readonly double[] Observations;
        /// <summary>Tracks the next exact observation supplied by a regression fixture.</summary>
        int ObservationIndex;
        /// <summary>Creates an incrementing clock or a finite sequence of explicit observations.</summary>
        public SceneLoadingTestClock(params double[] observations) { Observations = observations; }
        /// <summary>Returns the current observation and advances the synthetic clock.</summary>
        public double ElapsedMilliseconds {
            get {
                if (Observations.Length != 0) { return Observations[ObservationIndex++]; }
                double current = Next; Next += 1d; return current;
            }
        }
    }
}
