// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 4D vector of 32-bit integer components.
    /// </summary>
    public struct int4 {
        /// <summary>
        /// Identity-like vector with W set to one.
        /// </summary>
        private static readonly int4 identity = new int4(0, 0, 0, 1);

        /// <summary>
        /// X component of the vector.
        /// </summary>
        public int X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public int Y;

        /// <summary>
        /// Z component of the vector.
        /// </summary>
        public int Z;

        /// <summary>
        /// W component of the vector.
        /// </summary>
        public int W;

        /// <summary>
        /// Initializes a vector with specified component values.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        /// <param name="w">W component.</param>
        public int4(int x, int y, int z, int w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Gets a vector representing no rotation (identity).
        /// </summary>
        public static int4 Identity {
            get { return identity; }
        }

        /// <summary>
        /// Returns a string representing the vector components.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public override string ToString() {
            return $"{X}, {Y}, {Z}, {W}";
        }

        /// <summary>
        /// Determines whether a point lies within the bounds described by this vector.
        /// </summary>
        /// <param name="x">X coordinate to test.</param>
        /// <param name="y">Y coordinate to test.</param>
        /// <returns>True if the point is within the rectangle; otherwise false.</returns>
        public bool Contains(int x, int y) {
            return ((((X <= x) && (x < (X + Z))) && (Y <= y)) && (y < (Y + W)));
        }
    }
}
