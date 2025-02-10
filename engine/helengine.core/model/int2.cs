// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    public struct int2 {
        public int X;
        public int Y;


        /// <summary>
        /// Constructs a quaternion with X, Y from two values.
        /// </summary>
        /// <param name="x">The x coordinate in 3d-space.</param>
        /// <param name="y">The y coordinate in 3d-space.</param>
        public int2(int x, int y) {
            this.X = x;
            this.Y = y;
        }

        public override string ToString() {
            return $"{X}, {Y}";
        }
    }
}
