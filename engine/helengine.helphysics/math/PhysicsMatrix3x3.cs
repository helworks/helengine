namespace helengine {
    /// <summary>
    /// Represents a row-major three-by-three matrix used for rotation and inertia calculations.
    /// </summary>
    public readonly struct PhysicsMatrix3x3 {
        /// <summary>
        /// Stores the first matrix row.
        /// </summary>
        public readonly PhysicsVector3 Row0;

        /// <summary>
        /// Stores the second matrix row.
        /// </summary>
        public readonly PhysicsVector3 Row1;

        /// <summary>
        /// Stores the third matrix row.
        /// </summary>
        public readonly PhysicsVector3 Row2;

        /// <summary>
        /// Initializes a row-major matrix from its three rows.
        /// </summary>
        /// <param name="row0">First matrix row.</param>
        /// <param name="row1">Second matrix row.</param>
        /// <param name="row2">Third matrix row.</param>
        public PhysicsMatrix3x3(PhysicsVector3 row0, PhysicsVector3 row1, PhysicsVector3 row2) {
            Row0 = row0;
            Row1 = row1;
            Row2 = row2;
        }

        /// <summary>
        /// Gets the matrix that leaves every vector unchanged when transformed.
        /// </summary>
        public static PhysicsMatrix3x3 Identity => new PhysicsMatrix3x3(PhysicsVector3.UnitX, PhysicsVector3.UnitY, PhysicsVector3.UnitZ);

        /// <summary>
        /// Creates a diagonal matrix from the supplied diagonal components.
        /// </summary>
        /// <param name="diagonal">Values to place on the matrix diagonal.</param>
        /// <returns>A matrix whose non-diagonal entries are zero.</returns>
        public static PhysicsMatrix3x3 CreateDiagonal(PhysicsVector3 diagonal) {
            return new PhysicsMatrix3x3(
                new PhysicsVector3(diagonal.X, PhysicsScalar.Zero, PhysicsScalar.Zero),
                new PhysicsVector3(PhysicsScalar.Zero, diagonal.Y, PhysicsScalar.Zero),
                new PhysicsVector3(PhysicsScalar.Zero, PhysicsScalar.Zero, diagonal.Z));
        }

        /// <summary>
        /// Creates a rotation matrix from a quaternion.
        /// </summary>
        /// <param name="rotation">Non-zero quaternion representing the desired rotation.</param>
        /// <returns>The row-major rotation matrix for <paramref name="rotation"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rotation"/> has zero length.</exception>
        public static PhysicsMatrix3x3 CreateFromQuaternion(PhysicsQuaternion rotation) {
            PhysicsQuaternion normalized = rotation.Normalized();
            PhysicsScalar two = PhysicsScalar.FromFloat(2f);
            PhysicsScalar xx = normalized.X * normalized.X;
            PhysicsScalar yy = normalized.Y * normalized.Y;
            PhysicsScalar zz = normalized.Z * normalized.Z;
            PhysicsScalar xy = normalized.X * normalized.Y;
            PhysicsScalar xz = normalized.X * normalized.Z;
            PhysicsScalar yz = normalized.Y * normalized.Z;
            PhysicsScalar wx = normalized.W * normalized.X;
            PhysicsScalar wy = normalized.W * normalized.Y;
            PhysicsScalar wz = normalized.W * normalized.Z;

            return new PhysicsMatrix3x3(
                new PhysicsVector3(PhysicsScalar.One - (two * (yy + zz)), two * (xy - wz), two * (xz + wy)),
                new PhysicsVector3(two * (xy + wz), PhysicsScalar.One - (two * (xx + zz)), two * (yz - wx)),
                new PhysicsVector3(two * (xz - wy), two * (yz + wx), PhysicsScalar.One - (two * (xx + yy))));
        }

        /// <summary>
        /// Returns a matrix whose rows and columns are exchanged.
        /// </summary>
        /// <returns>The transpose of this matrix.</returns>
        public PhysicsMatrix3x3 Transposed() {
            return new PhysicsMatrix3x3(
                new PhysicsVector3(Row0.X, Row1.X, Row2.X),
                new PhysicsVector3(Row0.Y, Row1.Y, Row2.Y),
                new PhysicsVector3(Row0.Z, Row1.Z, Row2.Z));
        }

        /// <summary>
        /// Transforms a vector by this row-major matrix.
        /// </summary>
        /// <param name="vector">Vector to transform.</param>
        /// <returns>The matrix-vector product.</returns>
        public PhysicsVector3 Transform(PhysicsVector3 vector) {
            return new PhysicsVector3(
                PhysicsVector3.Dot(Row0, vector),
                PhysicsVector3.Dot(Row1, vector),
                PhysicsVector3.Dot(Row2, vector));
        }

        /// <summary>
        /// Multiplies two row-major matrices.
        /// </summary>
        /// <param name="left">Matrix applied second.</param>
        /// <param name="right">Matrix applied first.</param>
        /// <returns>The composed matrix product.</returns>
        public static PhysicsMatrix3x3 operator *(PhysicsMatrix3x3 left, PhysicsMatrix3x3 right) {
            PhysicsVector3 firstColumn = left.Transform(new PhysicsVector3(right.Row0.X, right.Row1.X, right.Row2.X));
            PhysicsVector3 secondColumn = left.Transform(new PhysicsVector3(right.Row0.Y, right.Row1.Y, right.Row2.Y));
            PhysicsVector3 thirdColumn = left.Transform(new PhysicsVector3(right.Row0.Z, right.Row1.Z, right.Row2.Z));

            return new PhysicsMatrix3x3(
                new PhysicsVector3(firstColumn.X, secondColumn.X, thirdColumn.X),
                new PhysicsVector3(firstColumn.Y, secondColumn.Y, thirdColumn.Y),
                new PhysicsVector3(firstColumn.Z, secondColumn.Z, thirdColumn.Z));
        }
    }
}
