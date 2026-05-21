#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT
namespace helengine {
    /// <summary>
    /// Resolves dynamic or static axis-aligned box contact information.
    /// </summary>
    public static class BoxBoxContactResolver3D {
        /// <summary>
        /// Finds the minimum-penetration axis for one overlapping box pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="penetration">Positive overlap distance on the selected axis.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>True when the boxes overlap.</returns>
        public static bool TryResolveContact(BodyState3D first, BodyState3D second, out float penetration, out int axisIndex) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            float xPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.X, first.AxisAlignedHalfExtents.X, second.Position.X, second.AxisAlignedHalfExtents.X);
            float yPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.Y, first.AxisAlignedHalfExtents.Y, second.Position.Y, second.AxisAlignedHalfExtents.Y);
            float zPenetration = PrimitiveContactMath3D.CalculateAxisPenetration(first.Position.Z, first.AxisAlignedHalfExtents.Z, second.Position.Z, second.AxisAlignedHalfExtents.Z);
            if (xPenetration <= 0f || yPenetration <= 0f || zPenetration <= 0f) {
                penetration = 0f;
                axisIndex = -1;
                return false;
            }

            if (xPenetration <= yPenetration && xPenetration <= zPenetration) {
                penetration = xPenetration;
                axisIndex = 0;
                return true;
            }
            if (yPenetration <= zPenetration) {
                penetration = yPenetration;
                axisIndex = 1;
                return true;
            }

            penetration = zPenetration;
            axisIndex = 2;
            return true;
        }
    }
}
#endif
