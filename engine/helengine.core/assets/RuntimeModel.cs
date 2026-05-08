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
        }

        /// <summary>
        /// Gets the runtime submesh draw ranges exposed by the model resource.
        /// </summary>
        public RuntimeSubmesh[] Submeshes { get; private set; }

        /// <summary>
        /// Gets the minimum vertex position observed when the runtime model was built.
        /// </summary>
        public float3 BoundsMin { get; private set; }

        /// <summary>
        /// Gets the maximum vertex position observed when the runtime model was built.
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
        /// Stores the model-space bounds captured while building the runtime resource.
        /// </summary>
        /// <param name="boundsMin">Minimum vertex position in model space.</param>
        /// <param name="boundsMax">Maximum vertex position in model space.</param>
        public void SetBounds(float3 boundsMin, float3 boundsMax) {
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }
    }
}
