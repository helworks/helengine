namespace helengine {
    /// <summary>
    /// Base class for entity components that participate in the engine lifecycle.
    /// </summary>
    public class Component : IDisposable {
        /// <summary>
        /// Tracks whether the component has completed disposal and should reject further use.
        /// </summary>
        bool isDisposed;
        /// <summary>
        /// Stores synthetic string members populated by platform-extended runtime payloads.
        /// </summary>
        readonly Dictionary<string, string> SyntheticStringMembers = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>
        /// Stores synthetic boolean members populated by platform-extended runtime payloads.
        /// </summary>
        readonly Dictionary<string, bool> SyntheticBooleanMembers = new Dictionary<string, bool>(StringComparer.Ordinal);
        /// <summary>
        /// Stores synthetic 32-bit integer members populated by platform-extended runtime payloads.
        /// </summary>
        readonly Dictionary<string, int> SyntheticInt32Members = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>
        /// Stores synthetic single-precision members populated by platform-extended runtime payloads.
        /// </summary>
        readonly Dictionary<string, float> SyntheticSingleMembers = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the entity this component is attached to.
        /// </summary>
        public Entity Parent { get; private set; }

        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that disables gameplay update execution during scene authoring.
        /// </summary>
        public virtual bool IsEditorUpdateExecutionSuppressionMarker => false;

        /// <summary>
        /// Gets whether this component is the editor-owned suppression marker that keeps one authored scene camera out of the runtime camera list during scene authoring.
        /// </summary>
        public virtual bool IsEditorSceneCameraSuppressionMarker => false;

        /// <summary>
        /// Gets the raw attached parent entity for internal lifecycle flows that must complete during disposal.
        /// </summary>
        internal Entity ParentUnsafe => Parent;

        /// <summary>
        /// Gets whether disposal has completed and the component should reject further use.
        /// </summary>
        internal bool IsDisposed => isDisposed;

        /// <summary>
        /// Stores one synthetic string member value on the component.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="value">String value to store.</param>
        public void SetSyntheticStringMember(string memberName, string value) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            SyntheticStringMembers[memberName] = value ?? string.Empty;
        }

        /// <summary>
        /// Resolves one synthetic string member value from the component or returns the supplied fallback when no value has been materialized.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="defaultValue">Fallback value returned when the member is absent.</param>
        /// <returns>Stored string value when present; otherwise the supplied fallback.</returns>
        public string GetSyntheticStringMemberOrDefault(string memberName, string defaultValue) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            if (SyntheticStringMembers.TryGetValue(memberName, out string value)) {
                return value;
            }

            return defaultValue ?? string.Empty;
        }

        /// <summary>
        /// Stores one synthetic boolean member value on the component.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="value">Boolean value to store.</param>
        public void SetSyntheticBooleanMember(string memberName, bool value) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            SyntheticBooleanMembers[memberName] = value;
        }

        /// <summary>
        /// Resolves one synthetic boolean member value from the component or returns the supplied fallback when no value has been materialized.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="defaultValue">Fallback value returned when the member is absent.</param>
        /// <returns>Stored boolean value when present; otherwise the supplied fallback.</returns>
        public bool GetSyntheticBooleanMemberOrDefault(string memberName, bool defaultValue) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            if (SyntheticBooleanMembers.TryGetValue(memberName, out bool value)) {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Stores one synthetic 32-bit integer member value on the component.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="value">Integer value to store.</param>
        public void SetSyntheticInt32Member(string memberName, int value) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            SyntheticInt32Members[memberName] = value;
        }

        /// <summary>
        /// Resolves one synthetic 32-bit integer member value from the component or returns the supplied fallback when no value has been materialized.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="defaultValue">Fallback value returned when the member is absent.</param>
        /// <returns>Stored integer value when present; otherwise the supplied fallback.</returns>
        public int GetSyntheticInt32MemberOrDefault(string memberName, int defaultValue) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            if (SyntheticInt32Members.TryGetValue(memberName, out int value)) {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Stores one synthetic single-precision member value on the component.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="value">Single-precision value to store.</param>
        public void SetSyntheticSingleMember(string memberName, float value) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            SyntheticSingleMembers[memberName] = value;
        }

        /// <summary>
        /// Resolves one synthetic single-precision member value from the component or returns the supplied fallback when no value has been materialized.
        /// </summary>
        /// <param name="memberName">Stable synthetic member name.</param>
        /// <param name="defaultValue">Fallback value returned when the member is absent.</param>
        /// <returns>Stored single-precision value when present; otherwise the supplied fallback.</returns>
        public float GetSyntheticSingleMemberOrDefault(string memberName, float defaultValue) {
            if (string.IsNullOrWhiteSpace(memberName)) {
                throw new ArgumentException("Synthetic member name must be provided.", nameof(memberName));
            }

            if (SyntheticSingleMembers.TryGetValue(memberName, out float value)) {
                return value;
            }

            return defaultValue;
        }

        /// <summary>
        /// Associates the component with one entity before any runtime lifecycle callbacks are considered.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        internal void AttachToEntity(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            ThrowIfDisposed();
            Parent = entity;
        }

        /// <summary>
        /// Clears the parent association after the component has finished its detach lifecycle.
        /// </summary>
        internal void DetachFromEntity() {
            Parent = null;
        }

        /// <summary>
        /// Throws when the component was already disposed and can no longer participate in runtime ownership flows.
        /// </summary>
        protected internal void ThrowIfDisposed() {
            if (isDisposed) {
                throw new InvalidOperationException("Disposed components cannot be used.");
            }
        }

        /// <summary>
        /// Called when the component is allowed to run its attach lifecycle.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public virtual void ComponentAdded(Entity entity) {
        }

        /// <summary>
        /// Called once after the parent entity hierarchy has finished initialization and the component can safely resolve related entities and components.
        /// </summary>
        /// <param name="entity">Entity that owns the initialized component.</param>
        public virtual void ComponentInitialized(Entity entity) {
        }

        /// <summary>
        /// Called when the component is allowed to run its detach lifecycle.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public virtual void ComponentRemoved(Entity entity) {
        }

        /// <summary>
        /// Called when the parent entity is enabled or disabled.
        /// </summary>
        /// <param name="newEnabled">True when enabled; false when disabled.</param>
        public virtual void ParentEnabledChange(bool newEnabled) {
        }

        /// <summary>
        /// Called when the parent entity toggles static state.
        /// </summary>
        /// <param name="newEnabled">True when marked static; otherwise false.</param>
        public virtual void ParentStaticChange(bool newEnabled) {
        }

        /// <summary>
        /// Releases runtime-owned resources held directly by the component before the native backend deletes the component instance.
        /// </summary>
        public virtual void Dispose() {
            isDisposed = true;
        }
    }
}
