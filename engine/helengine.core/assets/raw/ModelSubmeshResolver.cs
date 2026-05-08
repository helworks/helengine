namespace helengine {
    /// <summary>
    /// Resolves authored model submesh metadata into validated authored and runtime draw ranges.
    /// </summary>
    public static class ModelSubmeshResolver {
        /// <summary>
        /// Resolves the authored submesh collection for one model asset, synthesizing a default single submesh when none was authored.
        /// </summary>
        /// <param name="asset">Model asset whose submeshes should be resolved.</param>
        /// <returns>Validated authored submesh collection.</returns>
        public static ModelSubmeshAsset[] ResolveAssetSubmeshes(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            int elementCount = ResolveElementCount(asset);
            if (asset.Submeshes != null && asset.Submeshes.Length > 0) {
                ValidateSubmeshes(asset.Submeshes, elementCount);
                return asset.Submeshes;
            }
            if (elementCount == 0) {
                return Array.Empty<ModelSubmeshAsset>();
            }

            return new[] {
                new ModelSubmeshAsset {
                    MaterialSlotName = string.Empty,
                    IndexStart = 0,
                    IndexCount = elementCount
                }
            };
        }

        /// <summary>
        /// Builds runtime submesh metadata for one model asset.
        /// </summary>
        /// <param name="asset">Model asset whose runtime submeshes should be produced.</param>
        /// <returns>Runtime draw-range metadata derived from the authored asset.</returns>
        public static RuntimeSubmesh[] BuildRuntimeSubmeshes(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ModelSubmeshAsset[] authoredSubmeshes = ResolveAssetSubmeshes(asset);
            RuntimeSubmesh[] runtimeSubmeshes = new RuntimeSubmesh[authoredSubmeshes.Length];
            for (int submeshIndex = 0; submeshIndex < authoredSubmeshes.Length; submeshIndex++) {
                ModelSubmeshAsset authoredSubmesh = authoredSubmeshes[submeshIndex];
                runtimeSubmeshes[submeshIndex] = new RuntimeSubmesh {
                    MaterialSlotName = authoredSubmesh.MaterialSlotName ?? string.Empty,
                    IndexStart = authoredSubmesh.IndexStart,
                    IndexCount = authoredSubmesh.IndexCount
                };
            }

            return runtimeSubmeshes;
        }

        /// <summary>
        /// Resolves the total number of drawable elements in one model asset.
        /// </summary>
        /// <param name="asset">Model asset to inspect.</param>
        /// <returns>Total number of indexed or non-indexed drawable elements.</returns>
        static int ResolveElementCount(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ModelAssetIndexData indexData = ModelAssetIndexData.Resolve(asset);
            if (indexData.IndexCount > 0) {
                return indexData.IndexCount;
            }

            return asset.Positions == null ? 0 : asset.Positions.Length;
        }

        /// <summary>
        /// Validates one authored submesh collection against the resolved model draw range.
        /// </summary>
        /// <param name="submeshes">Submesh collection to validate.</param>
        /// <param name="elementCount">Total number of drawable elements available on the model.</param>
        static void ValidateSubmeshes(ModelSubmeshAsset[] submeshes, int elementCount) {
            if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }
            if (elementCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be zero or greater.");
            }

            for (int submeshIndex = 0; submeshIndex < submeshes.Length; submeshIndex++) {
                ModelSubmeshAsset submesh = submeshes[submeshIndex];
                if (submesh == null) {
                    throw new InvalidOperationException("Model submesh collections cannot contain null entries.");
                }
                if (submesh.IndexStart < 0) {
                    throw new InvalidOperationException("Model submesh index starts must be zero or greater.");
                }
                if (submesh.IndexCount <= 0) {
                    throw new InvalidOperationException("Model submesh index counts must be greater than zero.");
                }
                if (submesh.IndexStart + submesh.IndexCount > elementCount) {
                    throw new InvalidOperationException("Model submesh ranges cannot exceed the resolved model element count.");
                }
            }
        }
    }
}
