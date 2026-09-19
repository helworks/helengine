namespace helengine {
    /// <summary>
    /// Identifies one editor override scope as an ordered path of typed steps beneath Common. Common is the empty
    /// path. Paths are immutable value types and compare step by step with case-insensitive ids.
    /// </summary>
    public readonly struct EditorOverrideScope : IEquatable<EditorOverrideScope> {
        /// <summary>
        /// Platform id the properties panel uses for the shared Common tab. Equal to <c>ComponentPlatformEditingService.CommonPlatformId</c>.
        /// </summary>
        public const string CommonPlatformId = "common";

        /// <summary>
        /// The empty path.
        /// </summary>
        public static readonly EditorOverrideScope Common = new EditorOverrideScope(Array.Empty<EditorOverrideScopeStep>());

        readonly EditorOverrideScopeStep[] StepsValue;

        /// <summary>
        /// Builds the default-order path for a platform with an optional build-config beneath it. The common
        /// platform id, blank or otherwise, yields <see cref="Common"/>.
        /// </summary>
        public EditorOverrideScope(string platformId, string environmentId = null) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (string.Equals(platformId.Trim(), CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                StepsValue = Array.Empty<EditorOverrideScopeStep>();
                return;
            }

            if (string.IsNullOrWhiteSpace(environmentId)) {
                StepsValue = new[] { new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId) };
                return;
            }

            StepsValue = new[] {
                new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, platformId),
                new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, environmentId)
            };
        }

        /// <summary>
        /// Builds a path from explicit steps (copied).
        /// </summary>
        public EditorOverrideScope(IReadOnlyList<EditorOverrideScopeStep> steps) {
            if (steps == null || steps.Count == 0) {
                StepsValue = Array.Empty<EditorOverrideScopeStep>();
                return;
            }

            EditorOverrideScopeStep[] copy = new EditorOverrideScopeStep[steps.Count];
            for (int index = 0; index < steps.Count; index++) {
                copy[index] = steps[index];
            }

            StepsValue = copy;
        }

        /// <summary>Gets the steps from the first level beneath Common to this node.</summary>
        public IReadOnlyList<EditorOverrideScopeStep> Steps => StepsValue ?? Array.Empty<EditorOverrideScopeStep>();

        /// <summary>Gets the number of steps; zero for Common.</summary>
        public int Depth => Steps.Count;

        /// <summary>Gets whether this is the empty Common path.</summary>
        public bool IsCommon => Depth == 0;

        /// <summary>Gets the path one step shorter; Common's parent is Common.</summary>
        public EditorOverrideScope Parent {
            get {
                if (IsCommon) {
                    return Common;
                }

                EditorOverrideScopeStep[] parentSteps = new EditorOverrideScopeStep[Depth - 1];
                for (int index = 0; index < parentSteps.Length; index++) {
                    parentSteps[index] = Steps[index];
                }

                return new EditorOverrideScope(parentSteps);
            }
        }

        /// <summary>Returns a path with one more step.</summary>
        public EditorOverrideScope Append(EditorOverrideScopeStep step) {
            EditorOverrideScopeStep[] steps = new EditorOverrideScopeStep[Depth + 1];
            for (int index = 0; index < Depth; index++) {
                steps[index] = Steps[index];
            }

            steps[Depth] = step;
            return new EditorOverrideScope(steps);
        }

        /// <summary>Returns true when every step of this path matches the leading steps of <paramref name="other"/>.</summary>
        public bool IsPrefixOf(EditorOverrideScope other) {
            if (Depth > other.Depth) {
                return false;
            }

            for (int index = 0; index < Depth; index++) {
                if (!Steps[index].Equals(other.Steps[index])) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Returns the id of the first step of the requested kind.</summary>
        public bool TryGetStepId(SceneOverrideScopeStepKind kind, out string id) {
            for (int index = 0; index < Depth; index++) {
                if (Steps[index].Kind == kind) {
                    id = Steps[index].Id;
                    return true;
                }
            }

            id = string.Empty;
            return false;
        }

        /// <summary>Returns whether any step has the requested kind.</summary>
        public bool HasStepKind(SceneOverrideScopeStepKind kind) {
            return TryGetStepId(kind, out _);
        }

        /// <summary>Builds the one-step platform path, or Common for the common platform id.</summary>
        public static EditorOverrideScope ForPlatform(string platformId) {
            return new EditorOverrideScope(platformId);
        }

        /// <summary>Builds the default-order platform/build-config path.</summary>
        public static EditorOverrideScope ForPlatformBuildConfig(string platformId, string environmentId) {
            return new EditorOverrideScope(platformId, environmentId);
        }

        /// <summary>Builds a path from serialized steps.</summary>
        public static EditorOverrideScope FromSteps(SceneOverrideScopeStepAsset[] steps) {
            SceneOverrideScopeStepAsset[] normalized = SceneOverrideScopePath.Normalize(steps);
            EditorOverrideScopeStep[] editorSteps = new EditorOverrideScopeStep[normalized.Length];
            for (int index = 0; index < normalized.Length; index++) {
                editorSteps[index] = new EditorOverrideScopeStep(normalized[index].Kind, normalized[index].Id);
            }

            return new EditorOverrideScope(editorSteps);
        }

        /// <summary>Converts the path to serialized steps.</summary>
        public SceneOverrideScopeStepAsset[] ToSteps() {
            SceneOverrideScopeStepAsset[] steps = new SceneOverrideScopeStepAsset[Depth];
            for (int index = 0; index < Depth; index++) {
                steps[index] = Steps[index].ToAsset();
            }

            return steps;
        }

        /// <inheritdoc />
        public bool Equals(EditorOverrideScope other) {
            return IsPrefixOf(other) && Depth == other.Depth;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is EditorOverrideScope other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            HashCode hash = new HashCode();
            for (int index = 0; index < Depth; index++) {
                hash.Add(Steps[index]);
            }

            return hash.ToHashCode();
        }

        /// <inheritdoc />
        public override string ToString() {
            return SceneOverrideScopePath.Format(ToSteps());
        }

        /// <summary>Compares two scopes.</summary>
        public static bool operator ==(EditorOverrideScope left, EditorOverrideScope right) {
            return left.Equals(right);
        }

        /// <summary>Compares two scopes.</summary>
        public static bool operator !=(EditorOverrideScope left, EditorOverrideScope right) {
            return !left.Equals(right);
        }
    }
}
