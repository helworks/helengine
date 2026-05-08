namespace helengine {
    /// <summary>
    /// Represents a GPU-resident model resource.
    /// </summary>
    public abstract class RuntimeModel : RuntimeData {
        /// <summary>
        /// Initializes a new runtime model with an empty submesh collection.
        /// </summary>
        protected RuntimeModel() {
            Submeshes = Array.Empty<RuntimeSubmesh>();
            BoundsMin = float3.Zero;
            BoundsMax = float3.Zero;
        }

        /// <summary>
        /// Gets the runtime submesh draw ranges exposed by the model resource.
        /// </summary>
        public RuntimeSubmesh[] Submeshes { get; private set; }

        /// <summary>
        /// Gets the minimum corner of the model's cached axis-aligned bounds.
        /// </summary>
        public float3 BoundsMin { get; private set; }

        /// <summary>
        /// Gets the maximum corner of the model's cached axis-aligned bounds.
        /// </summary>
        public float3 BoundsMax { get; private set; }

        /// <summary>
        /// Replaces the runtime submesh collection exposed by the model resource.
        /// </summary>
        /// <param name="submeshes">Runtime submeshes that should be exposed by the model resource.</param>
        public void SetSubmeshes(RuntimeSubmesh[] submeshes) {
            if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }

            RuntimeSubmesh[] copiedSubmeshes = new RuntimeSubmesh[submeshes.Length];
            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                RuntimeSubmesh submesh = submeshes[submeshIndex];
                if (submesh == null) {
                    throw new InvalidOperationException("Runtime model submesh collections cannot contain null entries.");
                }

                copiedSubmeshes[submeshIndex] = submesh;
            }

            Submeshes = copiedSubmeshes;
        }

        /// <summary>
        /// Replaces the cached bounds exposed by the model resource.
        /// </summary>
        /// <param name="boundsMin">Minimum corner of the model bounds.</param>
        /// <param name="boundsMax">Maximum corner of the model bounds.</param>
        public void SetBounds(float3 boundsMin, float3 boundsMax) {
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }
    }
}
