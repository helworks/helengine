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
        /// Stable detached override member name that stores whether static render scale is baked into the cooked model variant.
        /// </summary>
        public const string BakeScaleMemberName = "MeshBakeScale";

        /// <summary>
        /// Stable detached override member name that selects package-time execution for enabled tessellation.
        /// </summary>
        public const string TessellateAtCookTimeMemberName = "MeshTessellateAtCookTime";

        /// <summary>
        /// Stable detached override member name that selects package-time execution for enabled scale baking.
        /// </summary>
        public const string BakeScaleAtCookTimeMemberName = "MeshBakeScaleAtCookTime";

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
                ReadTessellationMaxEdgeLength(overrideState),
                ReadBakeScale(overrideState),
                ReadTessellateAtCookTime(overrideState),
                ReadBakeScaleAtCookTime(overrideState));
        }

        /// <summary>
        /// Reads tessellation settings inherited by a platform or nested environment scope.
        /// </summary>
        public MeshComponentTessellationSettings GetForScope(EntityComponentSaveState saveState, EditorOverrideScope scope) {
            if (scope.IsPlatformOnly) {
                return GetForPlatform(saveState, scope.PlatformId);
            }

            MeshComponentTessellationSettings platformSettings = GetForPlatform(saveState, scope.PlatformId);
            if (!saveState.TryGetScopedPlatformOverride(scope, out EntityComponentPlatformOverrideState environmentState)) {
                return platformSettings;
            }

            return new MeshComponentTessellationSettings(
                ReadTessellateOr(environmentState, platformSettings.Tessellate),
                ReadTessellationMaxEdgeLengthOr(environmentState, platformSettings.TessellationMaxEdgeLength),
                ReadBakeScaleOr(environmentState, platformSettings.BakeScale),
                ReadBooleanOr(environmentState, TessellateAtCookTimeMemberName, platformSettings.TessellateAtCookTime),
                ReadBooleanOr(environmentState, BakeScaleAtCookTimeMemberName, platformSettings.BakeScaleAtCookTime));
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
            overrideState.SetMemberValue(BakeScaleMemberName, settings.BakeScale.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(TessellateAtCookTimeMemberName, settings.TessellateAtCookTime.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(BakeScaleAtCookTimeMemberName, settings.BakeScaleAtCookTime.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Stores tessellation settings at a platform or nested environment scope.
        /// </summary>
        public void SetForScope(EntityComponentSaveState saveState, EditorOverrideScope scope, MeshComponentTessellationSettings settings) {
            if (scope.IsPlatformOnly) {
                SetForPlatform(saveState, scope.PlatformId, settings);
                return;
            }
            ValidateSaveStateAndPlatformId(saveState, scope.PlatformId);
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreateScopedPlatformOverride(scope);
            overrideState.SetMemberValue(TessellateMemberName, settings.Tessellate.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(TessellationMaxEdgeLengthMemberName, settings.TessellationMaxEdgeLength.ToString("R", CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(BakeScaleMemberName, settings.BakeScale.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(TessellateAtCookTimeMemberName, settings.TessellateAtCookTime.ToString(CultureInfo.InvariantCulture));
            overrideState.SetMemberValue(BakeScaleAtCookTimeMemberName, settings.BakeScaleAtCookTime.ToString(CultureInfo.InvariantCulture));
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
                "BakeScale=" + settings.BakeScale.ToString(CultureInfo.InvariantCulture),
                "TessellateAtCookTime=" + settings.TessellateAtCookTime.ToString(CultureInfo.InvariantCulture),
                "BakeScaleAtCookTime=" + settings.BakeScaleAtCookTime.ToString(CultureInfo.InvariantCulture),
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

        bool ReadTessellateOr(EntityComponentPlatformOverrideState overrideState, bool fallback) {
            return overrideState.TryGetMemberValue(TessellateMemberName, out string value)
                ? bool.Parse(value)
                : fallback;
        }

        double ReadTessellationMaxEdgeLengthOr(EntityComponentPlatformOverrideState overrideState, double fallback) {
            return overrideState.TryGetMemberValue(TessellationMaxEdgeLengthMemberName, out string value)
                ? double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
                : fallback;
        }

        bool ReadBakeScaleOr(EntityComponentPlatformOverrideState overrideState, bool fallback) {
            return overrideState.TryGetMemberValue(BakeScaleMemberName, out string value)
                ? bool.Parse(value)
                : fallback;
        }

        bool ReadBooleanOr(EntityComponentPlatformOverrideState overrideState, string memberName, bool fallback) {
            return overrideState.TryGetMemberValue(memberName, out string value)
                ? bool.Parse(value)
                : fallback;
        }

        /// <summary>
        /// Reads the persisted scale-baking value or returns disabled when the detached member is absent.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the detached member.</param>
        /// <returns>The persisted scale-baking value or the disabled default.</returns>
        bool ReadBakeScale(EntityComponentPlatformOverrideState overrideState) {
            if (!overrideState.TryGetMemberValue(BakeScaleMemberName, out string value)) {
                return false;
            }
            if (!bool.TryParse(value, out bool bakeScale)) {
                throw new FormatException("MeshComponent scale baking enabled value is invalid.");
            }

            return bakeScale;
        }

        /// <summary>
        /// Reads the tessellation execution time or retains package-time execution for existing scenes without the detached member.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the detached member.</param>
        /// <returns>Whether enabled tessellation runs while packaging.</returns>
        bool ReadTessellateAtCookTime(EntityComponentPlatformOverrideState overrideState) {
            return ReadBooleanOrDefault(overrideState, TessellateAtCookTimeMemberName, true, "MeshComponent tessellation cook-time value is invalid.");
        }

        /// <summary>
        /// Reads the scale-baking execution time or retains package-time execution for existing scenes without the detached member.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the detached member.</param>
        /// <returns>Whether enabled scale baking runs while packaging.</returns>
        bool ReadBakeScaleAtCookTime(EntityComponentPlatformOverrideState overrideState) {
            return ReadBooleanOrDefault(overrideState, BakeScaleAtCookTimeMemberName, true, "MeshComponent scale baking cook-time value is invalid.");
        }

        /// <summary>
        /// Reads one detached Boolean member or returns its explicit compatibility default when absent.
        /// </summary>
        /// <param name="overrideState">Platform override metadata that may contain the member.</param>
        /// <param name="memberName">Stable detached member name.</param>
        /// <param name="defaultValue">Value used for scenes saved before the member existed.</param>
        /// <param name="invalidValueMessage">Exception message used when persisted text is invalid.</param>
        /// <returns>The parsed value or the compatibility default.</returns>
        bool ReadBooleanOrDefault(EntityComponentPlatformOverrideState overrideState, string memberName, bool defaultValue, string invalidValueMessage) {
            if (!overrideState.TryGetMemberValue(memberName, out string value)) {
                return defaultValue;
            }
            if (!bool.TryParse(value, out bool result)) {
                throw new FormatException(invalidValueMessage);
            }

            return result;
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
