namespace helengine {
    /// <summary>
    /// Allocates non-zero numeric entity ids for one serialized scene asset build.
    /// </summary>
    public sealed class SceneEntityAssetIdAllocator {
        /// <summary>
        /// Next non-zero entity id that will be assigned within the current scene build.
        /// </summary>
        uint NextEntityId;

        /// <summary>
        /// Initializes one allocator with the first valid non-zero scene-local id.
        /// </summary>
        public SceneEntityAssetIdAllocator() {
            NextEntityId = 1u;
        }

        /// <summary>
        /// Resets the allocator so a new scene build starts from the first valid scene-local id.
        /// </summary>
        public void Reset() {
            NextEntityId = 1u;
        }

        /// <summary>
        /// Allocates the next non-zero entity id for the current serialized scene build.
        /// </summary>
        /// <returns>Next scene-local entity id.</returns>
        public uint Allocate() {
            if (NextEntityId == 0u) {
                throw new InvalidOperationException("Scene entity id allocation overflowed.");
            }

            uint allocatedId = NextEntityId;
            NextEntityId++;
            return allocatedId;
        }
    }
}
