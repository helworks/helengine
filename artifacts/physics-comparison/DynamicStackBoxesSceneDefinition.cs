using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Provides the authored four-box dynamic stack scene data used by the city physics showcase.
    /// </summary>
    public static class DynamicStackBoxesSceneDefinition {
        /// <summary>
        /// Returns the static ground center position used by the authored scene.
        /// </summary>
        /// <returns>Ground center position.</returns>
        public static Vector3 CreateGroundPosition() {
            return new Vector3(0f, -0.5f, 0f);
        }

        /// <summary>
        /// Returns the static ground size used by the authored scene.
        /// </summary>
        /// <returns>Ground box size.</returns>
        public static Vector3 CreateGroundSize() {
            return new Vector3(14f, 1f, 14f);
        }

        /// <summary>
        /// Returns the dynamic box center positions used by the authored scene.
        /// </summary>
        /// <returns>Ordered box center positions.</returns>
        public static Vector3[] CreateBoxPositions() {
            return new[] {
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0f, 1.5f, 0f),
                new Vector3(0f, 2.5f, 0f),
                new Vector3(0f, 3.5f, 0f)
            };
        }

        /// <summary>
        /// Returns the stable trace name for one dynamic box index.
        /// </summary>
        /// <param name="bodyIndex">Zero-based dynamic box index.</param>
        /// <returns>Stable trace label.</returns>
        public static string CreateBodyName(int bodyIndex) {
            return "box" + (bodyIndex + 1).ToString("00");
        }
    }
}
