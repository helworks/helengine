namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Provides deterministic host-frame delta sequences used to compare runtime playback against fixed-step references.
    /// </summary>
    public static class FrameReplaySequenceLibrary {
        /// <summary>
        /// Creates one host-frame sequence with a single large hitch followed by steady sixty-hertz updates.
        /// </summary>
        /// <returns>Ordered host-frame delta sequence in seconds.</returns>
        public static float[] CreateSingleHitchRecoverySequence() {
            float[] frameDeltas = new float[91];
            frameDeltas[0] = 0.5f;
            for (int index = 1; index < frameDeltas.Length; index++) {
                frameDeltas[index] = 1f / 60f;
            }

            return frameDeltas;
        }
    }
}
