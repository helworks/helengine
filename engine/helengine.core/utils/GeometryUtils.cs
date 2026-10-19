namespace helengine {
    /// <summary>
    /// Provides common geometry helpers used across engine systems.
    /// </summary>
    public class GeometryUtils {
        /// <summary>
        /// Determines whether a point lies within a rectangle defined by an origin and size.
        /// </summary>
        /// <param name="x">X coordinate to test.</param>
        /// <param name="y">Y coordinate to test.</param>
        /// <param name="origin">Top-left origin of the rectangle.</param>
        /// <param name="width">Rectangle width.</param>
        /// <param name="height">Rectangle height.</param>
        /// <returns>True when the point is inside the rectangle.</returns>
        public static bool IsPointInsideRect(RealEvaluation x, RealEvaluation y, float3 origin, int width, int height) {
            RealEvaluation left = origin.X;
            RealEvaluation top = origin.Y;
            RealEvaluation right = left + width;
            RealEvaluation bottom = top + height;

            return x >= left && x < right && y >= top && y < bottom;
        }
    }
}
