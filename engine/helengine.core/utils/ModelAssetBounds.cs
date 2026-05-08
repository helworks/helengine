namespace helengine {
    /// <summary>
    /// Computes axis-aligned bounds for model assets from their authored vertex positions.
    /// </summary>
    public static class ModelAssetBounds {
        /// <summary>
        /// Applies computed bounds to one model asset in place.
        /// </summary>
        /// <param name="asset">Model asset whose bounds should be derived from its positions.</param>
        public static void Apply(ModelAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            Resolve(asset.Positions, out float3 boundsMin, out float3 boundsMax);
            asset.BoundsMin = boundsMin;
            asset.BoundsMax = boundsMax;
        }

        /// <summary>
        /// Resolves axis-aligned bounds for one position buffer.
        /// </summary>
        /// <param name="positions">Vertex positions to evaluate.</param>
        /// <param name="boundsMin">Minimum corner of the computed bounds.</param>
        /// <param name="boundsMax">Maximum corner of the computed bounds.</param>
        public static void Resolve(float3[] positions, out float3 boundsMin, out float3 boundsMax) {
            if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            } else if (positions.Length == 0) {
                throw new InvalidOperationException("Model assets must contain at least one vertex position to compute bounds.");
            }

            double minX = positions[0].X;
            double minY = positions[0].Y;
            double minZ = positions[0].Z;
            double maxX = positions[0].X;
            double maxY = positions[0].Y;
            double maxZ = positions[0].Z;

            for (int positionIndex = 1; positionIndex < positions.Length; positionIndex++) {
                float3 position = positions[positionIndex];
                if (position.X < minX) {
                    minX = position.X;
                }
                if (position.Y < minY) {
                    minY = position.Y;
                }
                if (position.Z < minZ) {
                    minZ = position.Z;
                }
                if (position.X > maxX) {
                    maxX = position.X;
                }
                if (position.Y > maxY) {
                    maxY = position.Y;
                }
                if (position.Z > maxZ) {
                    maxZ = position.Z;
                }
            }

            boundsMin = new float3((float)minX, (float)minY, (float)minZ);
            boundsMax = new float3((float)maxX, (float)maxY, (float)maxZ);
        }
    }
}
