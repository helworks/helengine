namespace helengine {
    /// <summary>
    /// Builds, normalizes, compares and formats override scope paths. The empty path is Common. Shared by the
    /// runtime reader, the editor file format and the editor so every layer agrees on path identity.
    /// </summary>
    public static class SceneOverrideScopePath {
        /// <summary>
        /// Label used when the empty Common path is formatted.
        /// </summary>
        public const string CommonLabel = "common";

        /// <summary>
        /// Returns the empty Common path.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Common() {
            return Array.Empty<SceneOverrideScopeStepAsset>();
        }

        /// <summary>
        /// Builds a one-step path for a platform.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Platform(string platformId) {
            return new[] { CreateStep(SceneOverrideScopeStepKind.Platform, platformId) };
        }

        /// <summary>
        /// Builds a one-step path for a group.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Group(string groupId) {
            return new[] { CreateStep(SceneOverrideScopeStepKind.Group, groupId) };
        }

        /// <summary>
        /// Builds a platform path with an optional build-config step beneath it; a blank environment yields the platform-only path.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] PlatformBuildConfig(string platformId, string environmentId) {
            if (string.IsNullOrWhiteSpace(environmentId)) {
                return Platform(platformId);
            }

            return new[] {
                CreateStep(SceneOverrideScopeStepKind.Platform, platformId),
                CreateStep(SceneOverrideScopeStepKind.BuildConfig, environmentId)
            };
        }

        /// <summary>
        /// Returns a copy with null steps removed and ids trimmed; a step with a blank id is a data error.
        /// </summary>
        public static SceneOverrideScopeStepAsset[] Normalize(SceneOverrideScopeStepAsset[] steps) {
            if (steps == null) {
                return new SceneOverrideScopeStepAsset[0];
            }

            int count = 0;
            for (int index = 0; index < steps.Length; index++) {
                if (steps[index] != null) {
                    count++;
                }
            }

            SceneOverrideScopeStepAsset[] normalized = new SceneOverrideScopeStepAsset[count];
            int write = 0;
            for (int index = 0; index < steps.Length; index++) {
                SceneOverrideScopeStepAsset step = steps[index];
                if (step == null) {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(step.Id)) {
                    throw new InvalidOperationException("Override scope steps must define a non-blank id.");
                }

                normalized[write] = CreateStep(step.Kind, step.Id);
                write++;
            }

            return normalized;
        }

        /// <summary>
        /// Formats a path as <c>kind:id/kind:id</c>, or <see cref="CommonLabel"/> for the empty path.
        /// </summary>
        public static string Format(SceneOverrideScopeStepAsset[] steps) {
            if (steps == null || steps.Length == 0) {
                return CommonLabel;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int index = 0; index < steps.Length; index++) {
                SceneOverrideScopeStepAsset step = steps[index];
                if (step == null) {
                    continue;
                }
                if (builder.Length > 0) {
                    builder.Append('/');
                }

                builder.Append(FormatKind(step.Kind));
                builder.Append(':');
                builder.Append((step.Id ?? string.Empty).Trim());
            }

            return builder.Length == 0 ? CommonLabel : builder.ToString();
        }

        /// <summary>
        /// Compares two paths step by step, ignoring id case and surrounding whitespace. Null equals the empty path.
        /// </summary>
        public static bool AreEqual(SceneOverrideScopeStepAsset[] left, SceneOverrideScopeStepAsset[] right) {
            SceneOverrideScopeStepAsset[] normalizedLeft = Normalize(left);
            SceneOverrideScopeStepAsset[] normalizedRight = Normalize(right);
            if (normalizedLeft.Length != normalizedRight.Length) {
                return false;
            }

            for (int index = 0; index < normalizedLeft.Length; index++) {
                if (normalizedLeft[index].Kind != normalizedRight[index].Kind
                    || !string.Equals(normalizedLeft[index].Id, normalizedRight[index].Id, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Lower-case kind label used by <see cref="Format"/>.
        /// </summary>
        public static string FormatKind(SceneOverrideScopeStepKind kind) {
            if (kind == SceneOverrideScopeStepKind.Group) {
                return "group";
            }
            if (kind == SceneOverrideScopeStepKind.Platform) {
                return "platform";
            }

            return "buildconfig";
        }

        /// <summary>
        /// Creates one trimmed step; blank ids are a programming error.
        /// </summary>
        static SceneOverrideScopeStepAsset CreateStep(SceneOverrideScopeStepKind kind, string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Override scope step id must be provided.", nameof(id));
            }

            return new SceneOverrideScopeStepAsset {
                Kind = kind,
                Id = id.Trim()
            };
        }
    }
}
