namespace helengine {
    /// <summary>Shares camera-relative transparent ordering between immediate and extracted render paths.</summary>
    public static class CameraDepthOrder {
        /// <summary>Compares transparent drawables within their authored 3D bands using far-to-near view depth.</summary>
        /// <param name="left">First drawable.</param>
        /// <param name="right">Second drawable.</param>
        /// <param name="forward">Camera forward direction in world space.</param>
        /// <returns>Negative when the first drawable should be composited earlier.</returns>
        public static int CompareTransparent(IDrawable3D left, IDrawable3D right, float3 forward) {
            int order = left.RenderOrder3D.CompareTo(right.RenderOrder3D);
            if (order != 0) {
                return order;
            }
            Entity leftEntity = left.Parent;
            Entity rightEntity = right.Parent;
            if (leftEntity == null || rightEntity == null) {
                return 0;
            }
            float3 leftPosition = leftEntity.Position;
            float3 rightPosition = rightEntity.Position;
            double leftDepth = (double)leftPosition.X * forward.X + (double)leftPosition.Y * forward.Y + (double)leftPosition.Z * forward.Z;
            double rightDepth = (double)rightPosition.X * forward.X + (double)rightPosition.Y * forward.Y + (double)rightPosition.Z * forward.Z;
            int depthOrder = rightDepth.CompareTo(leftDepth);
            if (depthOrder != 0) {
                return depthOrder;
            }
            Entity leftSource = left is IRenderHierarchySource leftProxy ? leftProxy.SourceEntity : leftEntity;
            Entity rightSource = right is IRenderHierarchySource rightProxy ? rightProxy.SourceEntity : rightEntity;
            return RenderDepthOrder2D.CompareHierarchy(leftSource, rightSource);
        }

        /// <summary>Identifies immediate drawables containing alpha-blended material content.</summary>
        /// <param name="drawable">Drawable whose material slots are examined.</param>
        /// <returns>True when at least one material needs transparent composition.</returns>
        public static bool IsTransparent(IDrawable3D drawable) {
            RuntimeMaterial[] materials = drawable.Materials;
            if (materials == null) {
                return false;
            }
            for (int index = 0; index < materials.Length; index++) {
                RuntimeMaterial material = materials[index];
                if (material != null && material.RenderState != null && material.RenderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                    return true;
                }
            }
            return false;
        }
    }
}
