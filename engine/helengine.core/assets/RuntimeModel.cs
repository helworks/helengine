namespace helengine {
    /// <summary>
    /// Represents a GPU-resident model resource.
    /// </summary>
    public abstract class RuntimeModel : RuntimeData, IDisposable {
        /// <summary>
        /// Initializes a new runtime model with an empty submesh collection.
        /// </summary>
        protected RuntimeModel() {
            SubmeshesValue = new RuntimeSubmesh[0];
        }

        /// <summary>
        /// Runtime submesh draw ranges exposed by this model resource.
        /// </summary>
        [NativeOwnedMember]
        RuntimeSubmesh[] SubmeshesValue;

        /// <summary>
        /// Raw model geometry retained only when a renderer supports load-time mesh preparation.
        /// </summary>
        [NativeOwnedMember]
        ModelAsset RawModelAssetValue;

        /// <summary>
        /// Gets the runtime submesh draw ranges exposed by the model resource.
        /// </summary>
        public RuntimeSubmesh[] Submeshes {
            get => SubmeshesValue;
        }

        /// <summary>
        /// Gets raw geometry retained beside this runtime model for private load-time preparation copies.
        /// </summary>
        public ModelAsset RawModelAsset => RawModelAssetValue;

        /// <summary>
        /// Gets the minimum vertex position observed when the runtime model was built.
        /// </summary>
        public float3 BoundsMin { get; private set; }

        /// <summary>
        /// Gets the maximum vertex position observed when the runtime model was built.
        /// </summary>
        public float3 BoundsMax { get; private set; }

        /// <summary>
        /// Releases model-owned submesh containers that native builds cannot reclaim through the top-level object delete alone.
        /// </summary>
        public virtual void Dispose() {
            NativeOwnership.DeleteItemsAndRelease(ref SubmeshesValue);
            NativeOwnership.DisposeAndRelease(ref RawModelAssetValue);
        }

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

            NativeOwnership.DeleteItemsAndRelease(ref SubmeshesValue);
            SubmeshesValue = copiedSubmeshes;
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

        /// <summary>
        /// Transfers ownership of one raw model asset retained for later load-time geometry preparation.
        /// </summary>
        /// <param name="rawModelAsset">Raw model asset whose ownership transfers to this runtime model.</param>
        public void SetRawModelAsset([NativeTakesOwnership] ModelAsset rawModelAsset) {
            if (rawModelAsset == null) {
                throw new ArgumentNullException(nameof(rawModelAsset));
            }

            NativeOwnership.DisposeAndDelete(RawModelAssetValue);
            RawModelAssetValue = rawModelAsset;
        }
    }
}
