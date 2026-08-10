namespace helengine {
    /// <summary>
    /// Identifies one editor override scope. Environment scopes are always owned by a platform.
    /// </summary>
    public readonly struct EditorOverrideScope : IEquatable<EditorOverrideScope> {
        /// <summary>
        /// Initializes one platform-only or platform/environment scope.
        /// </summary>
        /// <param name="platformId">Owning platform identifier.</param>
        /// <param name="environmentId">Optional environment identifier nested under the platform.</param>
        public EditorOverrideScope(string platformId, string environmentId = null) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            PlatformId = platformId.Trim();
            EnvironmentId = string.IsNullOrWhiteSpace(environmentId) ? string.Empty : environmentId.Trim();
        }

        /// <summary>
        /// Gets the owning platform identifier.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the nested environment identifier, or an empty string for the platform-only scope.
        /// </summary>
        public string EnvironmentId { get; }

        /// <summary>
        /// Gets a value indicating whether this identifies the platform-only override.
        /// </summary>
        public bool IsPlatformOnly => string.IsNullOrEmpty(EnvironmentId);

        /// <summary>
        /// Compares two scopes case-insensitively by their normalized identifiers.
        /// </summary>
        public bool Equals(EditorOverrideScope other) {
            return string.Equals(PlatformId, other.PlatformId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(EnvironmentId, other.EnvironmentId, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is EditorOverrideScope other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(PlatformId ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(EnvironmentId ?? string.Empty));
        }

        /// <inheritdoc />
        public override string ToString() {
            return IsPlatformOnly ? PlatformId : $"{PlatformId}/{EnvironmentId}";
        }

        /// <summary>
        /// Compares two scopes.
        /// </summary>
        public static bool operator ==(EditorOverrideScope left, EditorOverrideScope right) {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two scopes.
        /// </summary>
        public static bool operator !=(EditorOverrideScope left, EditorOverrideScope right) {
            return !left.Equals(right);
        }
    }
}
