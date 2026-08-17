namespace helengine.editor {
    /// <summary>
    /// Maintains a rolling average of editor frame deltas for the viewport stats overlay.
    /// </summary>
    public sealed class EditorViewportFrameRateTracker {
        /// <summary>
        /// Rolling frame-delta samples in seconds.
        /// </summary>
        readonly double[] Samples;

        /// <summary>
        /// Index that receives the next recorded sample.
        /// </summary>
        int NextSampleIndex;

        /// <summary>
        /// Number of valid samples currently stored.
        /// </summary>
        int SampleCount;

        /// <summary>
        /// Initializes one frame-rate tracker with the supplied rolling window size.
        /// </summary>
        /// <param name="windowSize">Number of frame samples averaged by the tracker.</param>
        public EditorViewportFrameRateTracker(int windowSize) {
            if (windowSize < 1) {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Frame window size must be at least one sample.");
            }

            Samples = new double[windowSize];
        }

        /// <summary>
        /// Gets the average frames-per-second across the current window, or zero before any valid sample exists.
        /// </summary>
        public double AverageFps {
            get {
                double averageSeconds = AverageFrameSeconds;
                return averageSeconds <= 0.0 ? 0.0 : 1.0 / averageSeconds;
            }
        }

        /// <summary>
        /// Gets the average frame duration in milliseconds across the current window, or zero before any valid sample exists.
        /// </summary>
        public double AverageFrameMilliseconds {
            get { return AverageFrameSeconds * 1000.0; }
        }

        /// <summary>
        /// Gets the average frame duration in seconds across the current window, or zero before any valid sample exists.
        /// </summary>
        double AverageFrameSeconds {
            get {
                if (SampleCount == 0) {
                    return 0.0;
                }

                double total = 0.0;
                for (int index = 0; index < SampleCount; index++) {
                    total += Samples[index];
                }

                return total / SampleCount;
            }
        }

        /// <summary>
        /// Records one frame delta; non-positive deltas are ignored.
        /// </summary>
        /// <param name="deltaSeconds">Frame duration in seconds.</param>
        public void Record(double deltaSeconds) {
            if (deltaSeconds <= 0.0) {
                return;
            }

            Samples[NextSampleIndex] = deltaSeconds;
            NextSampleIndex = (NextSampleIndex + 1) % Samples.Length;
            if (SampleCount < Samples.Length) {
                SampleCount++;
            }
        }
    }
}
