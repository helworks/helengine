namespace helengine.editor.assimp {
    /// <summary>
    /// Matrix helpers for turning imported node matrices into translation, rotation and scale.
    /// </summary>
    public static class AssimpTransformUtils {
        /// <summary>
        /// Axis lengths at or below this value are treated as a collapsed (zero-scale) axis.
        /// </summary>
        const float DegenerateAxisLength = 1e-8f;

        /// <summary>
        /// Builds the normal transform of a (row-vector) matrix from the cofactors of its upper 3x3 block. Each row is the
        /// cross product of the other two basis rows, so it equals the inverse transpose scaled by the determinant and still
        /// exists when a zero scale collapsed an axis: normals of a flattened mesh turn to the collapsed axis, and normals of a
        /// mesh collapsed to a point or a line become zero vectors, which the callers leave unnormalized.
        /// </summary>
        /// <param name="matrix">Matrix whose rows are the scaled basis axes.</param>
        /// <returns>Normal transform with no translation.</returns>
        public static System.Numerics.Matrix4x4 CreateCofactorNormalTransform(System.Numerics.Matrix4x4 matrix) {
            System.Numerics.Vector3 axisX = new System.Numerics.Vector3(matrix.M11, matrix.M12, matrix.M13);
            System.Numerics.Vector3 axisY = new System.Numerics.Vector3(matrix.M21, matrix.M22, matrix.M23);
            System.Numerics.Vector3 axisZ = new System.Numerics.Vector3(matrix.M31, matrix.M32, matrix.M33);
            System.Numerics.Vector3 cofactorX = System.Numerics.Vector3.Cross(axisY, axisZ);
            System.Numerics.Vector3 cofactorY = System.Numerics.Vector3.Cross(axisZ, axisX);
            System.Numerics.Vector3 cofactorZ = System.Numerics.Vector3.Cross(axisX, axisY);
            return new System.Numerics.Matrix4x4(
                cofactorX.X, cofactorX.Y, cofactorX.Z, 0f,
                cofactorY.X, cofactorY.Y, cofactorY.Z, 0f,
                cofactorZ.X, cofactorZ.Y, cofactorZ.Z, 0f,
                0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// Decomposes a System.Numerics (row-vector) matrix, tolerating axes collapsed by a zero scale.
        /// </summary>
        /// <param name="matrix">Matrix whose rows are the scaled basis axes and whose last row is the translation.</param>
        /// <param name="scale">Receives the per-axis scale; a collapsed axis reports 0.</param>
        /// <param name="rotation">Receives the rotation.</param>
        /// <param name="translation">Receives the translation.</param>
        public static void Decompose(System.Numerics.Matrix4x4 matrix, out System.Numerics.Vector3 scale, out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation) {
            System.Numerics.Vector3 axisX = new System.Numerics.Vector3(matrix.M11, matrix.M12, matrix.M13);
            System.Numerics.Vector3 axisY = new System.Numerics.Vector3(matrix.M21, matrix.M22, matrix.M23);
            System.Numerics.Vector3 axisZ = new System.Numerics.Vector3(matrix.M31, matrix.M32, matrix.M33);
            float lengthX = axisX.Length();
            float lengthY = axisY.Length();
            float lengthZ = axisZ.Length();
            bool hasX = lengthX > DegenerateAxisLength;
            bool hasY = lengthY > DegenerateAxisLength;
            bool hasZ = lengthZ > DegenerateAxisLength;
            if (hasX && hasY && hasZ) {
                if (!System.Numerics.Matrix4x4.Decompose(matrix, out scale, out rotation, out translation)) {
                    throw new InvalidOperationException("Node matrix cannot be decomposed into translation, rotation and scale.");
                }
                return;
            }

            translation = new System.Numerics.Vector3(matrix.M41, matrix.M42, matrix.M43);
            scale = new System.Numerics.Vector3(hasX ? lengthX : 0f, hasY ? lengthY : 0f, hasZ ? lengthZ : 0f);

            // With fewer than two surviving axes the orientation is unrecoverable (the matrix has no direction left), so identity is the only honest answer.
            int surviving = (hasX ? 1 : 0) + (hasY ? 1 : 0) + (hasZ ? 1 : 0);
            if (surviving < 2) {
                rotation = System.Numerics.Quaternion.Identity;
                return;
            }

            // Two axes survive: orthonormalize them and rebuild the collapsed one with a cross product so the basis stays right-handed.
            System.Numerics.Vector3 first = hasX ? axisX / lengthX : axisY / lengthY;
            System.Numerics.Vector3 secondRaw = hasZ ? axisZ / lengthZ : axisY / lengthY;
            System.Numerics.Vector3 second = secondRaw - System.Numerics.Vector3.Dot(secondRaw, first) * first;
            if (second.Length() <= DegenerateAxisLength) {
                rotation = System.Numerics.Quaternion.Identity;
                return;
            }
            second = System.Numerics.Vector3.Normalize(second);

            System.Numerics.Vector3 basisX;
            System.Numerics.Vector3 basisY;
            System.Numerics.Vector3 basisZ;
            if (!hasZ) {
                basisX = first;
                basisY = second;
                basisZ = System.Numerics.Vector3.Cross(first, second);
            } else if (!hasY) {
                basisZ = second;
                basisX = first;
                basisY = System.Numerics.Vector3.Cross(second, first);
            } else {
                basisY = first;
                basisZ = second;
                basisX = System.Numerics.Vector3.Cross(first, second);
            }

            System.Numerics.Matrix4x4 rotationMatrix = new System.Numerics.Matrix4x4(
                basisX.X, basisX.Y, basisX.Z, 0f,
                basisY.X, basisY.Y, basisY.Z, 0f,
                basisZ.X, basisZ.Y, basisZ.Z, 0f,
                0f, 0f, 0f, 1f);
            rotation = System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromRotationMatrix(rotationMatrix));
        }
    }
}
