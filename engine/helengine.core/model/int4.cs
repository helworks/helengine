// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    public struct int4 {
        private static readonly int4 _identity = new int4(0, 0, 0, 1);

        public int X;
        public int Y;
        public int Z;
        public int W;

        /// <summary>
        /// Returns a quaternion representing no rotation.
        /// </summary>
        public static int4 Identity {
            get { return _identity; }
        }

        /// <summary>
        /// Constructs a quaternion with X, Y, Z and W from four values.
        /// </summary>
        /// <param name="x">The x coordinate in 3d-space.</param>
        /// <param name="y">The y coordinate in 3d-space.</param>
        /// <param name="z">The z coordinate in 3d-space.</param>
        /// <param name="w">The rotation component.</param>
        public int4(int x, int y, int z, int w) {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.W = w;
        }

        public override string ToString() {
            return $"{X}, {Y}, {Z}, {W}";
        }

        /// <summary>
        /// Gets whether or not the provided coordinates lie within the bounds of this <see cref="int4"/>.
        /// </summary>
        /// <param name="x">The x coordinate of the point to check for containment.</param>
        /// <param name="y">The y coordinate of the point to check for containment.</param>
        /// <returns><c>true</c> if the provided coordinates lie inside this <see cref="int4"/>; <c>false</c> otherwise.</returns>
        public bool Contains(int x, int y) {
            return ((((this.X <= x) && (x < (this.X + this.Z))) && (this.Y <= y)) && (y < (this.Y + this.W)));
        }
    }
}
