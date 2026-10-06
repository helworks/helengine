namespace helengine {
    /// <summary>Shares camera-relative transparent ordering between immediate and extracted render paths.</summary>
    public static class CameraDepthOrder {
        /// <summary>Compares transparent drawables within their authored 3D bands using far-to-near view depth.</summary>
        /// <param name="left">First drawable.</param>
        /// <param name="right">Second drawable.</param>
        /// <param name="forward">Camera forward direction in world space.</param>
        /// <param name="cameraPosition">World origin of the camera's central viewing ray.</param>
        /// <returns>Negative when the first drawable should be composited earlier.</returns>
        public static int CompareTransparent(IDrawable3D left, IDrawable3D right, float3 forward, float3 cameraPosition = default) {
            int order = left.RenderOrder3D.CompareTo(right.RenderOrder3D);
            if (order != 0) {
                return order;
            }
            Entity leftEntity = left.Parent;
            Entity rightEntity = right.Parent;
            if (leftEntity == null || rightEntity == null) {
                return 0;
            }
            double leftDepth = ResolveDepth(left, forward, cameraPosition);
            double rightDepth = ResolveDepth(right, forward, cameraPosition);
            int depthOrder = rightDepth.CompareTo(leftDepth);
            if (depthOrder != 0) {
                return depthOrder;
            }
            Entity leftSource = left is IRenderHierarchySource leftProxy ? leftProxy.SourceEntity : leftEntity;
            Entity rightSource = right is IRenderHierarchySource rightProxy ? rightProxy.SourceEntity : rightEntity;
            return RenderDepthOrder2D.CompareHierarchy(leftSource, rightSource);
        }

        /// <summary>
        /// Measures a plane on the camera's central ray so in-plane XY offsets cannot change its composition order.
        /// Nonplanar meshes and edge-on planes retain the usual projected-origin depth.
        /// </summary>
        /// <param name="drawable">Drawable whose world geometry supplies the depth.</param>
        /// <param name="forward">Camera forward vector.</param>
        /// <param name="cameraPosition">Camera world position.</param>
        /// <returns>Signed camera-relative depth used consistently for all comparisons.</returns>
        static double ResolveDepth(IDrawable3D drawable, float3 forward, float3 cameraPosition) {
            float3 offset = drawable.Parent.Position - cameraPosition;
            if (drawable is IPlanarDrawable3D plane) {
                float3 normal = plane.WorldPlaneNormal;
                double denominator = (double)normal.X * forward.X + (double)normal.Y * forward.Y + (double)normal.Z * forward.Z;
                if (Math.Abs(denominator) > 0.000001) {
                    return ((double)normal.X * offset.X + (double)normal.Y * offset.Y + (double)normal.Z * offset.Z) / denominator;
                }
            }
            return (double)offset.X * forward.X + (double)offset.Y * forward.Y + (double)offset.Z * forward.Z;
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
