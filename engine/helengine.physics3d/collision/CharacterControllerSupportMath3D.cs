#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_BODY_SUPPORT || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_STATIC_MESH_SUPPORT
namespace helengine {
    /// <summary>
    /// Provides shared math helpers used by character-controller support resolvers.
    /// </summary>
    public static class CharacterControllerSupportMath3D {
        /// <summary>
        /// Calculates the minimum upward-facing surface dot product required for a walkable slope angle.
        /// </summary>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <returns>Minimum acceptable upward-facing surface dot product.</returns>
        public static float CalculateMinimumWalkableSurfaceDot(double maximumSlopeDegrees) {
            double radians = maximumSlopeDegrees * Math.PI / 180d;
            return (float)Math.Cos(radians);
        }
    }
}
#endif
