using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Reads, writes, and identifies editor-only MeshComponent tessellation metadata stored in per-platform component override state.
    /// </summary>
    public sealed class MeshComponentTessellationSettingsService {
        /// <summary>
        /// Stable detached override member name that stores whether component-specific tessellation is enabled.
        /// </summary>
        public const string TessellateMemberName = "MeshTessellate";

        /// <summary>
        /// Stable detached override member name that stores the component maximum world-space edge length.
        /// </summary>
        public const string TessellationMaxEdgeLengthMemberName = "MeshTessellationMaxEdgeLength";

        /// <summary>
        /// Reads the editor-only MeshComponent tessellation settings for one target platform without creating missing override metadata.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Target platform identifier whose settings should be resolved.</param>
        /// <returns>An immutable settings snapshot for the target platform.</returns>
        public MeshComponentTessellationSettings GetForPlatform(EntityComponentSaveState saveState, string platformId) {
            ValidateSaveStateAndPlatformId(saveState, platformId);

            if (!saveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return new MeshComponentTessellationSettings();
            }

            return new MeshComponentTessellationSettings(
                ReadTessellate(overrideState),
                ReadTessellationMaxEdgeLength(overrideState));
        }

        /// <summary>
        /// Stores editor-only MeshComponent tessellation settings in the selected platform override metadata.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Target platform identifier that owns the settings.</param>
        /// <param name="settings">Validated settings snapshot to persist.</param>
        public void SetForPlatform(EntityComponentSaveState saveState, string platformId, MeshComponentTessellationSettings settings) {
            ValidateSaveStateAndPlatformId(saveState, platformId);
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride(platformId);
            overrideState.SetMemberValue(TessellateMemberName, settings.Tessellate.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(
                TessellationMaxEdgeLengthMemberName,
                settings.TessellationMaxEdgeLength.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Builds a stable identity for one component-specific cooked tessellation model variant.
        /// </summary>
        /// <param name="sourceModelReference">Platform-resolved source model reference used as the variant input.</param>
        /// <param name="platformId">Target platform identifier that owns the variant.</param>
        /// <param name="settings">Enabled or disabled component tessellation settings.</param>
        /// <param name="worldScale">Final static world scale used only to measure local model edges.</param>
        /// <returns>A newline-delimited invariant identity suitable for deterministic hashing.</returns>
        public string BuildVariantIdentity(
            string sourceModelReference,
            string platformId,
            MeshComponentTessellationSettings settings,
            float3 worldScale) {
            if (string.IsNullOrWhiteSpace(sourceModelReference)) {
                throw new ArgumentException("Source model reference must be provided.", nameof(sourceModelReference));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            ValidateWorldScale(worldScale);
            return string.Join(
                "\n",
                "SourceModelReference=" + sourceModelReference,
                "PlatformId=" + platformId,
                "Tessellate=" + settings.Tessellate.ToString(CultureInfo.InvariantCulture),
                "TessellationMaxEdgeLength=" + settings.TessellationMaxEdgeLength.ToString("R", CultureInfo.InvariantCulture),
                "WorldScaleX=" + worldScale.X.ToString("R", CultureInfo.InvariantCulture),
                "WorldScaleY=" + worldScale.Y.ToString("R", CultureInfo.InvariantCulture),
                "WorldScaleZ=" + worldScale.Z.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Reads the persisted enabled value or returns the disabled default when the detached member is absent.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the detached member.</param>
        /// <returns>The persisted enabled value or the disabled default.</returns>
        bool ReadTessellate(EntityComponentPlatformOverrideState overrideState) {
            if (!overrideState.TryGetMemberValue(TessellateMemberName, out string value)) {
                return false;
            }
            if (!bool.TryParse(value, out bool tessellate)) {
                throw new FormatException("MeshComponent tessellation enabled value is invalid.");
            }

            return tessellate;
        }

        /// <summary>
        /// Reads the persisted maximum edge length or returns the standard default when the detached member is absent.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the detached member.</param>
        /// <returns>The persisted maximum edge length or its default.</returns>
        double ReadTessellationMaxEdgeLength(EntityComponentPlatformOverrideState overrideState) {
            if (!overrideState.TryGetMemberValue(TessellationMaxEdgeLengthMemberName, out string value)) {
                return MeshComponentTessellationSettings.DefaultTessellationMaxEdgeLength;
            }
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tessellationMaxEdgeLength)) {
                throw new FormatException("MeshComponent tessellation maximum edge length is invalid.");
            }

            MeshComponentTessellationSettings.ValidateTessellationMaxEdgeLength(tessellationMaxEdgeLength);
            return tessellationMaxEdgeLength;
        }

        /// <summary>
        /// Validates a component save state and selected platform identifier before accessing detached metadata.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for one MeshComponent.</param>
        /// <param name="platformId">Target platform identifier that owns the metadata.</param>
        void ValidateSaveStateAndPlatformId(EntityComponentSaveState saveState, string platformId) {
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
        }

        /// <summary>
        /// Validates the final static world scale used to measure component model edges.
        /// </summary>
        /// <param name="worldScale">Final static world scale used for world-space edge measurement.</param>
        void ValidateWorldScale(float3 worldScale) {
            if (!float.IsFinite(worldScale.X) || worldScale.X == 0f) {
                throw new ArgumentException("World scale X must be finite and non-zero.", nameof(worldScale));
            } else if (!float.IsFinite(worldScale.Y) || worldScale.Y == 0f) {
                throw new ArgumentException("World scale Y must be finite and non-zero.", nameof(worldScale));
            } else if (!float.IsFinite(worldScale.Z) || worldScale.Z == 0f) {
                throw new ArgumentException("World scale Z must be finite and non-zero.", nameof(worldScale));
            }
        }
    }
}
