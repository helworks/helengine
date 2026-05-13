namespace helengine.editor {
    /// <summary>
    /// Allocates editor-owned numeric scene entity ids for one loaded editor scene session.
    /// </summary>
    public sealed class EditorSceneEntityIdAllocator {
        /// <summary>
        /// Next scene entity id that will be assigned.
        /// </summary>
        uint NextEntityId = 1u;

        /// <summary>
        /// Allocates one fresh non-zero scene entity id.
        /// </summary>
        /// <returns>Fresh numeric scene entity id.</returns>
        public uint Allocate() {
            if (NextEntityId == 0u) {
                throw new InvalidOperationException("Scene entity id space is exhausted.");
            }

            uint allocatedId = NextEntityId;
            NextEntityId++;
            return allocatedId;
        }

        /// <summary>
        /// Registers one restored scene entity id and advances the allocator beyond it when needed.
        /// </summary>
        /// <param name="entityId">Restored scene entity id.</param>
        public void RegisterRestored(uint entityId) {
            if (entityId == 0u) {
                throw new ArgumentException("Scene entity id must be non-zero.", nameof(entityId));
            }

            if (entityId >= NextEntityId) {
                NextEntityId = entityId + 1u;
            }
        }
    }
}
