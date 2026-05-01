// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 2D vector of 32-bit integer components.
    /// </summary>
    public struct int2 {
        /// <summary>
        /// Gets the zero vector with both components set to <c>0</c>.
        /// </summary>
        public static readonly int2 Zero = new int2(0, 0);

        /// <summary>
        /// X component of the vector.
        /// </summary>
        public int X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public int Y;

        /// <summary>
        /// Initializes a vector with the specified components.
        /// </summary>
        /// <param name="x">Value for the X component.</param>
        /// <param name="y">Value for the Y component.</param>
        public int2(int x, int y) {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns a string representing the vector components.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public override string ToString() {
            return $"{X}, {Y}";
        }
    }
}
