using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Reads and writes the editor-only MeshComponent modifier stack stored in per-scope component override metadata.
    /// </summary>
    public sealed class MeshComponentModifierStackService {
        /// <summary>
        /// Stable detached member name that stores the number of stack entries in one scope.
        /// </summary>
        public const string ModifierCountMemberName = "MeshModifierCount";

        /// <summary>
        /// Stable detached member-name prefix for per-entry modifier metadata.
        /// </summary>
        public const string ModifierMemberNamePrefix = "MeshModifier";

        /// <summary>
        /// Shared legacy tessellation settings service used for backward-compatible reads.
        /// </summary>
        readonly MeshComponentTessellationSettingsService TessellationSettingsService = new MeshComponentTessellationSettingsService();

        /// <summary>
        /// Reads the modifier stack authored directly in one scope, without common-scope inheritance.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Platform identifier (or the common platform id) whose stack should be read.</param>
        /// <returns>The authored stack, or <c>null</c> when the scope carries no stack metadata.</returns>
        public List<MeshComponentModifier> TryGetAuthoredStack(EntityComponentSaveState saveState, string platformId) {
            ValidateSaveStateAndPlatformId(saveState, platformId);
            if (!saveState.TryGetPlatformOverride(platformId, out EntityComponentPlatformOverrideState overrideState)) {
                return null;
            }

            return TryReadStack(overrideState);
        }

        /// <summary>
        /// Resolves the effective modifier stack for one platform: the platform-authored stack, falling back to
        /// platform-authored legacy tessellation members, then to the common-scope stack.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <returns>Effective ordered modifier stack; empty when nothing is authored anywhere.</returns>
        public List<MeshComponentModifier> ResolveEffectiveStack(EntityComponentSaveState saveState, string platformId) {
            ValidateSaveStateAndPlatformId(saveState, platformId);

            List<MeshComponentModifier> platformStack = TryGetAuthoredStack(saveState, platformId);
            if (platformStack != null) {
                return platformStack;
            }

            List<MeshComponentModifier> legacyStack = TryReadLegacyTessellationStack(saveState, platformId);
            if (legacyStack != null) {
                return legacyStack;
            }

            if (!string.Equals(platformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                List<MeshComponentModifier> commonStack = TryGetAuthoredStack(saveState, ComponentPlatformEditingService.CommonPlatformId);
                if (commonStack != null) {
                    return commonStack;
                }
            }

            return new List<MeshComponentModifier>();
        }

        /// <summary>
        /// Stores one modifier stack in the supplied scope, replacing any previously authored entries.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Platform identifier (or the common platform id) that owns the stack.</param>
        /// <param name="modifiers">Ordered modifier entries to persist.</param>
        public void SetStack(EntityComponentSaveState saveState, string platformId, IReadOnlyList<MeshComponentModifier> modifiers) {
            ValidateSaveStateAndPlatformId(saveState, platformId);
            if (modifiers == null) {
                throw new ArgumentNullException(nameof(modifiers));
            }

            EntityComponentPlatformOverrideState overrideState = saveState.GetOrCreatePlatformOverride(platformId);
            SynchronizeLegacyTessellationMembers(saveState, platformId, modifiers);
            overrideState.SetMemberValue(ModifierCountMemberName, modifiers.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < modifiers.Count; index++) {
                MeshComponentModifier modifier = modifiers[index];
                if (modifier == null) {
                    throw new InvalidOperationException("Modifier stacks cannot contain null entries.");
                }

                overrideState.SetMemberValue(BuildMemberName(index, "Kind"), modifier.Kind);
                overrideState.SetMemberValue(BuildMemberName(index, "MaxEdgeLength"), modifier.MaxEdgeLength.ToString("R", CultureInfo.InvariantCulture));
                overrideState.SetMemberValue(BuildMemberName(index, "AtCookTime"), modifier.AtCookTime.ToString(CultureInfo.InvariantCulture));
                overrideState.SetMemberValue(BuildMemberName(index, "Preview"), modifier.Preview.ToString(CultureInfo.InvariantCulture));
                overrideState.SetMemberValue(BuildMemberName(index, "UvwMode"), modifier.UvwMode ?? ModelUvwMapProcessor.BoxMode);
                overrideState.SetMemberValue(BuildMemberName(index, "UvwPlane"), modifier.UvwPlane ?? ModelUvwMapProcessor.PlaneXZ);
                overrideState.SetMemberValue(BuildMemberName(index, "UvwScale"), modifier.UvwScale.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Resolves the first tessellation modifier in the effective stack for one platform as legacy tessellation settings.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <returns>Legacy-compatible tessellation settings when a tessellation modifier applies; otherwise <c>null</c>.</returns>
        public MeshComponentTessellationSettings TryResolveTessellationSettings(EntityComponentSaveState saveState, string platformId) {
            List<MeshComponentModifier> effectiveStack = ResolveEffectiveStack(saveState, platformId);
            for (int index = 0; index < effectiveStack.Count; index++) {
                MeshComponentModifier modifier = effectiveStack[index];
                if (!string.Equals(modifier.Kind, MeshComponentModifier.TessellateKind, StringComparison.Ordinal)) {
                    continue;
                }

                return new MeshComponentTessellationSettings(
                    tessellate: true,
                    tessellationMaxEdgeLength: modifier.MaxEdgeLength,
                    bakeScale: false,
                    tessellateAtCookTime: modifier.AtCookTime,
                    bakeScaleAtCookTime: true);
            }

            return null;
        }

        /// <summary>
        /// Reads the stack entries stored in one override state.
        /// </summary>
        /// <param name="overrideState">Override state that may carry stack metadata.</param>
        /// <returns>The authored entries, or <c>null</c> when the scope carries no stack metadata.</returns>
        List<MeshComponentModifier> TryReadStack(EntityComponentPlatformOverrideState overrideState) {
            if (!overrideState.TryGetMemberValue(ModifierCountMemberName, out string countText)) {
                return null;
            }
            if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0) {
                throw new FormatException("MeshComponent modifier count is invalid.");
            }

            List<MeshComponentModifier> modifiers = new List<MeshComponentModifier>(count);
            for (int index = 0; index < count; index++) {
                if (!overrideState.TryGetMemberValue(BuildMemberName(index, "Kind"), out string kind) || string.IsNullOrWhiteSpace(kind)) {
                    throw new FormatException($"MeshComponent modifier {index} does not declare a kind.");
                }

                MeshComponentModifier modifier = new MeshComponentModifier(kind);
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "MaxEdgeLength"), out string maxEdgeLengthText)) {
                    modifier.MaxEdgeLength = double.Parse(maxEdgeLengthText, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "AtCookTime"), out string atCookTimeText)) {
                    modifier.AtCookTime = bool.Parse(atCookTimeText);
                }
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "Preview"), out string previewText)) {
                    modifier.Preview = bool.Parse(previewText);
                }
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "UvwMode"), out string uvwModeText)) {
                    modifier.UvwMode = uvwModeText;
                }
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "UvwPlane"), out string uvwPlaneText)) {
                    modifier.UvwPlane = uvwPlaneText;
                }
                if (overrideState.TryGetMemberValue(BuildMemberName(index, "UvwScale"), out string uvwScaleText)) {
                    modifier.UvwScale = double.Parse(uvwScaleText, NumberStyles.Float, CultureInfo.InvariantCulture);
                }

                modifiers.Add(modifier);
            }

            return modifiers;
        }

        /// <summary>
        /// Maps legacy per-platform tessellation members onto one single-entry modifier stack.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <returns>Single-entry stack when legacy tessellation is enabled; otherwise <c>null</c>.</returns>
        List<MeshComponentModifier> TryReadLegacyTessellationStack(EntityComponentSaveState saveState, string platformId) {
            if (string.Equals(platformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            MeshComponentTessellationSettings settings = TessellationSettingsService.GetForPlatform(saveState, platformId);
            if (!settings.Tessellate) {
                return null;
            }

            return new List<MeshComponentModifier> {
                new MeshComponentModifier(MeshComponentModifier.TessellateKind) {
                    MaxEdgeLength = settings.TessellationMaxEdgeLength,
                    AtCookTime = settings.TessellateAtCookTime
                }
            };
        }

        /// <summary>
        /// Mirrors the stack's first tessellation modifier into the legacy per-platform tessellation members so
        /// existing cook and load-time readers stay authoritative and never shadow newer stack edits.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for the MeshComponent.</param>
        /// <param name="platformId">Scope identifier receiving the stack.</param>
        /// <param name="modifiers">Ordered modifier entries being persisted.</param>
        void SynchronizeLegacyTessellationMembers(EntityComponentSaveState saveState, string platformId, IReadOnlyList<MeshComponentModifier> modifiers) {
            if (string.Equals(platformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            MeshComponentModifier tessellateModifier = null;
            for (int index = 0; index < modifiers.Count; index++) {
                if (modifiers[index] != null && string.Equals(modifiers[index].Kind, MeshComponentModifier.TessellateKind, StringComparison.Ordinal)) {
                    tessellateModifier = modifiers[index];
                    break;
                }
            }

            MeshComponentTessellationSettings legacySettings = tessellateModifier == null
                ? new MeshComponentTessellationSettings()
                : new MeshComponentTessellationSettings(
                    tessellate: true,
                    tessellationMaxEdgeLength: tessellateModifier.MaxEdgeLength,
                    bakeScale: false,
                    tessellateAtCookTime: tessellateModifier.AtCookTime,
                    bakeScaleAtCookTime: true);
            TessellationSettingsService.SetForPlatform(saveState, platformId, legacySettings);
        }

        /// <summary>
        /// Builds one stable per-entry member name.
        /// </summary>
        /// <param name="index">Zero-based stack entry index.</param>
        /// <param name="suffix">Member suffix identifying the stored field.</param>
        /// <returns>Stable detached member name.</returns>
        static string BuildMemberName(int index, string suffix) {
            return ModifierMemberNamePrefix + index.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        /// <summary>
        /// Validates a component save state and scope identifier before accessing detached metadata.
        /// </summary>
        /// <param name="saveState">Editor persistence metadata for one MeshComponent.</param>
        /// <param name="platformId">Scope identifier that owns the metadata.</param>
        static void ValidateSaveStateAndPlatformId(EntityComponentSaveState saveState, string platformId) {
            if (saveState == null) {
                throw new ArgumentNullException(nameof(saveState));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
        }
    }
}
