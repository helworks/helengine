namespace helengine {
    /// <summary>
    /// Tracks runtime body handles for the currently bound scene and resolves them by owning entity in constant time.
    /// </summary>
    public sealed class BepuBodyRegistry3D {
        /// <summary>
        /// Registered runtime body handles in deterministic scene-binding order.
        /// </summary>
        readonly List<BepuBodyHandle3D> HandlesValue = new List<BepuBodyHandle3D>();

        /// <summary>
        /// Maps each owning entity to the runtime body handle registered for it so lookups avoid a linear scan.
        /// </summary>
        readonly Dictionary<Entity, BepuBodyHandle3D> HandlesByEntityValue = new Dictionary<Entity, BepuBodyHandle3D>();

        /// <summary>
        /// Gets the registered runtime body handles.
        /// </summary>
        public IReadOnlyList<BepuBodyHandle3D> Handles => HandlesValue;

        /// <summary>
        /// Clears the registry and the entity lookup that mirrors it.
        /// </summary>
        public void Clear() {
            HandlesValue.Clear();
            HandlesByEntityValue.Clear();
        }

        /// <summary>
        /// Adds one runtime body handle and mirrors it into the entity lookup.
        /// </summary>
        /// <param name="handle">Handle to add.</param>
        public void Add(BepuBodyHandle3D handle) {
            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }

            HandlesValue.Add(handle);
            // The scan this lookup replaced returned the first matching handle, so later duplicates stay shadowed.
            if (!HandlesByEntityValue.ContainsKey(handle.Entity)) {
                HandlesByEntityValue.Add(handle.Entity, handle);
            }
        }

        /// <summary>
        /// Resolves the runtime body handle registered for one entity.
        /// </summary>
        /// <param name="entity">Entity whose runtime body handle should be resolved.</param>
        /// <returns>Registered handle when the entity is bound; otherwise null.</returns>
        [NativeBorrowedReturn]
        public BepuBodyHandle3D FindHandle(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            BepuBodyHandle3D handle;
            if (HandlesByEntityValue.TryGetValue(entity, out handle)) {
                return handle;
            }

            return null;
        }
    }
}
