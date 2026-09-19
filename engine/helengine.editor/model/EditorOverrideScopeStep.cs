namespace helengine {
    /// <summary>
    /// One step on an editor override scope path. Ids compare case-insensitively.
    /// </summary>
    public readonly struct EditorOverrideScopeStep : IEquatable<EditorOverrideScopeStep> {
        /// <summary>
        /// Initializes one step with a trimmed id.
        /// </summary>
        public EditorOverrideScopeStep(SceneOverrideScopeStepKind kind, string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Override scope step id must be provided.", nameof(id));
            }

            Kind = kind;
            Id = id.Trim();
        }

        /// <summary>Gets the level kind of this step.</summary>
        public SceneOverrideScopeStepKind Kind { get; }

        /// <summary>Gets the node id at this step.</summary>
        public string Id { get; }

        /// <summary>Converts the step to its serialized record.</summary>
        public SceneOverrideScopeStepAsset ToAsset() {
            return new SceneOverrideScopeStepAsset { Kind = Kind, Id = Id };
        }

        /// <inheritdoc />
        public bool Equals(EditorOverrideScopeStep other) {
            return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return obj is EditorOverrideScopeStep other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            return HashCode.Combine((int)Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty));
        }
    }
}
