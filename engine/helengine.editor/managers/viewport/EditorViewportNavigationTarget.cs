namespace helengine.editor {
    /// <summary>
    /// Immutable signed axis direction selected on the viewport navigation cube.
    /// </summary>
    public sealed class EditorViewportNavigationTarget : IEquatable<EditorViewportNavigationTarget> {
        /// <summary>
        /// Initializes one non-zero cube direction whose components each range from negative one to positive one.
        /// </summary>
        /// <param name="x">Signed camera offset along world X.</param>
        /// <param name="y">Signed camera offset along world Y.</param>
        /// <param name="z">Signed camera offset along world Z.</param>
        public EditorViewportNavigationTarget(int x, int y, int z) {
            if (x < -1 || x > 1) {
                throw new ArgumentOutOfRangeException(nameof(x), x, "Navigation target components must be -1, 0, or 1.");
            }
            if (y < -1 || y > 1) {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Navigation target components must be -1, 0, or 1.");
            }
            if (z < -1 || z > 1) {
                throw new ArgumentOutOfRangeException(nameof(z), z, "Navigation target components must be -1, 0, or 1.");
            }
            if (x == 0 && y == 0 && z == 0) {
                throw new ArgumentException("A navigation target must point toward at least one cube face.");
            }

            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the signed camera offset along world X.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the signed camera offset along world Y.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the signed camera offset along world Z.
        /// </summary>
        public int Z { get; }

        /// <summary>
        /// Gets whether this target represents one of the six cube faces.
        /// </summary>
        public bool IsFace => GetActiveAxisCount() == 1;

        /// <summary>
        /// Gets whether this target represents one of the twelve cube edges.
        /// </summary>
        public bool IsEdge => GetActiveAxisCount() == 2;

        /// <summary>
        /// Gets whether this target represents one of the eight cube corners.
        /// </summary>
        public bool IsCorner => GetActiveAxisCount() == 3;

        /// <summary>
        /// Compares this target with another target using its three signed axis components.
        /// </summary>
        /// <param name="other">Target to compare.</param>
        /// <returns>True when all three components match.</returns>
        public bool Equals(EditorViewportNavigationTarget other) {
            return other != null && X == other.X && Y == other.Y && Z == other.Z;
        }

        /// <summary>
        /// Compares this target with any object of the same value.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True when the object is an equal navigation target.</returns>
        public override bool Equals(object obj) {
            return Equals(obj as EditorViewportNavigationTarget);
        }

        /// <summary>
        /// Gets a stable hash code from the three signed axis components.
        /// </summary>
        /// <returns>Hash code representing this target.</returns>
        public override int GetHashCode() {
            return HashCode.Combine(X, Y, Z);
        }

        /// <summary>
        /// Counts the non-zero axis components in this direction.
        /// </summary>
        /// <returns>Number of axes participating in the direction.</returns>
        int GetActiveAxisCount() {
            int activeAxisCount = 0;
            if (X != 0) {
                activeAxisCount++;
            }
            if (Y != 0) {
                activeAxisCount++;
            }
            if (Z != 0) {
                activeAxisCount++;
            }

            return activeAxisCount;
        }
    }
}
