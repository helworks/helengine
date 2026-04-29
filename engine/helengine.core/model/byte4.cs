// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 4-component vector of bytes, often used for colors.
    /// </summary>
    public struct byte4 {
        /// <summary>
        /// X component of the vector.
        /// </summary>
        public byte X;

        /// <summary>
        /// Y component of the vector.
        /// </summary>
        public byte Y;

        /// <summary>
        /// Z component of the vector.
        /// </summary>
        public byte Z;

        /// <summary>
        /// W component of the vector.
        /// </summary>
        public byte W;

        /// <summary>
        /// Initializes a vector with the specified components.
        /// </summary>
        /// <param name="x">X component.</param>
        /// <param name="y">Y component.</param>
        /// <param name="z">Z component.</param>
        /// <param name="w">W component.</param>
        public byte4(byte x, byte y, byte z, byte w) {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Returns a string representing the vector components.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public override string ToString() {
            return $"{X}, {Y}, {Z}, {W}";
        }
    }
}
