using System.Text;

namespace helengine {
    /// <summary>
    /// Formats compact scene-memory probe checkpoints for stable runtime log output.
    /// </summary>
    public static class SceneMemoryProbeLogFormatter {
        /// <summary>
        /// Formats one scene-memory probe checkpoint into a stable single-line log record.
        /// </summary>
        /// <param name="measurement">Checkpoint payload that should be formatted for logging.</param>
        /// <returns>Stable single-line log record.</returns>
        public static string Format(SceneMemoryProbeMeasurement measurement) {
            if (measurement == null) {
                throw new ArgumentNullException(nameof(measurement));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("[SceneMemoryProbe] probe=");
            builder.Append(measurement.ProbeName);
            builder.Append(" cycle=");
            builder.Append(FormatInt32(measurement.CycleIndex));
            builder.Append(" step=");
            builder.Append(FormatInt32(measurement.StepIndex));
            builder.Append(" label=");
            builder.Append(measurement.Label);
            builder.Append(" action=");
            builder.Append(FormatActionKind(measurement.ActionKind));
            builder.Append(" resident_bytes=");
            builder.Append(FormatUInt64(measurement.ResidentBytes));
            builder.Append(" committed_bytes=");
            builder.Append(FormatUInt64(measurement.CommittedBytes));
            builder.Append(" scenes=");
            builder.Append(measurement.LoadedSceneIds);
            builder.Append(" drawables2d=");
            builder.Append(FormatInt32(measurement.Drawables2DCount));
            builder.Append(" drawables3d=");
            builder.Append(FormatInt32(measurement.Drawables3DCount));
            builder.Append(" draw_calls=");
            builder.Append(FormatInt32(measurement.DrawCallCount));
            builder.Append(" owned_textures=");
            builder.Append(FormatInt32(measurement.ActiveOwnedTextureCount));
            builder.Append(" owned_fonts=");
            builder.Append(FormatInt32(measurement.ActiveOwnedFontCount));
            builder.Append(" owned_models=");
            builder.Append(FormatInt32(measurement.ActiveOwnedModelCount));
            builder.Append(" owned_materials=");
            builder.Append(FormatInt32(measurement.ActiveOwnedMaterialCount));
            return builder.ToString();
        }

        /// <summary>
        /// Formats one authored probe action into the stable token written into the runtime log line.
        /// </summary>
        /// <param name="actionKind">Probe action that should be formatted.</param>
        /// <returns>Stable action token written into the checkpoint line.</returns>
        static string FormatActionKind(SceneMemoryProbeActionKind actionKind) {
            if (actionKind == SceneMemoryProbeActionKind.Wait) {
                return "Wait";
            } else if (actionKind == SceneMemoryProbeActionKind.LoadSceneSingle) {
                return "LoadSceneSingle";
            } else if (actionKind == SceneMemoryProbeActionKind.LoadSceneAdditive) {
                return "LoadSceneAdditive";
            } else if (actionKind == SceneMemoryProbeActionKind.UnloadScene) {
                return "UnloadScene";
            }

            return ((int)actionKind).ToString();
        }

        /// <summary>
        /// Formats one unsigned 64-bit counter into a stable decimal token that can be appended by every runtime backend.
        /// </summary>
        /// <param name="value">Unsigned 64-bit value that should be formatted.</param>
        /// <returns>Stable decimal string representation.</returns>
        static string FormatUInt64(ulong value) {
            return value.ToString();
        }

        /// <summary>
        /// Formats one signed 32-bit counter into a stable decimal token that can be appended by every runtime backend.
        /// </summary>
        /// <param name="value">Signed 32-bit value that should be formatted.</param>
        /// <returns>Stable decimal string representation.</returns>
        static string FormatInt32(int value) {
            return value.ToString();
        }
    }
}
