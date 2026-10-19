// MIT License - Copyright (C) The Mono.Xna Team
// Portions of this file are based on work by The Mono.Xna Team and are subject to
// the terms and conditions defined in file 'LICENSE.txt', which is part of this source code package.
//
// Additional modifications and work by Helena.

namespace helengine {
    /// <summary>
    /// Represents a 4x4 matrix of build-configurable Real elements and related math helpers.
    /// </summary>
    public struct float4x4 {
        /// <summary>
        /// Matrix element at first row, first column.
        /// </summary>
        public Real M11;
        /// <summary>
        /// Matrix element at first row, second column.
        /// </summary>
        public Real M12;
        /// <summary>
        /// Matrix element at first row, third column.
        /// </summary>
        public Real M13;
        /// <summary>
        /// Matrix element at first row, fourth column.
        /// </summary>
        public Real M14;
        /// <summary>
        /// Matrix element at second row, first column.
        /// </summary>
        public Real M21;
        /// <summary>
        /// Matrix element at second row, second column.
        /// </summary>
        public Real M22;
        /// <summary>
        /// Matrix element at second row, third column.
        /// </summary>
        public Real M23;
        /// <summary>
        /// Matrix element at second row, fourth column.
        /// </summary>
        public Real M24;
        /// <summary>
        /// Matrix element at third row, first column.
        /// </summary>
        public Real M31;
        /// <summary>
        /// Matrix element at third row, second column.
        /// </summary>
        public Real M32;
        /// <summary>
        /// Matrix element at third row, third column.
        /// </summary>
        public Real M33;
        /// <summary>
        /// Matrix element at third row, fourth column.
        /// </summary>
        public Real M34;
        /// <summary>
        /// Matrix element at fourth row, first column.
        /// </summary>
        public Real M41;
        /// <summary>
        /// Matrix element at fourth row, second column.
        /// </summary>
        public Real M42;
        /// <summary>
        /// Matrix element at fourth row, third column.
        /// </summary>
        public Real M43;
        /// <summary>
        /// Matrix element at fourth row, fourth column.
        /// </summary>
        public Real M44;

        /// <summary>
        /// Backing field for the identity matrix value.
        /// </summary>
        private static float4x4 IdentityValue = new float4x4(1f, 0f, 0f, 0f,
                                            0f, 1f, 0f, 0f,
                                            0f, 0f, 1f, 0f,
                                            0f, 0f, 0f, 1f);

        /// <summary>
        /// Constructs a matrix.
        /// </summary>
        /// <param name="m11">A first row and first column value.</param>
        /// <param name="m12">A first row and second column value.</param>
        /// <param name="m13">A first row and third column value.</param>
        /// <param name="m14">A first row and fourth column value.</param>
        /// <param name="m21">A second row and first column value.</param>
        /// <param name="m22">A second row and second column value.</param>
        /// <param name="m23">A second row and third column value.</param>
        /// <param name="m24">A second row and fourth column value.</param>
        /// <param name="m31">A third row and first column value.</param>
        /// <param name="m32">A third row and second column value.</param>
        /// <param name="m33">A third row and third column value.</param>
        /// <param name="m34">A third row and fourth column value.</param>
        /// <param name="m41">A fourth row and first column value.</param>
        /// <param name="m42">A fourth row and second column value.</param>
        /// <param name="m43">A fourth row and third column value.</param>
        /// <param name="m44">A fourth row and fourth column value.</param>
        public float4x4(Real m11, Real m12, Real m13, Real m14, Real m21, Real m22, Real m23, Real m24, Real m31,
                      Real m32, Real m33, Real m34, Real m41, Real m42, Real m43, Real m44) {
            this.M11 = m11;
            this.M12 = m12;
            this.M13 = m13;
            this.M14 = m14;
            this.M21 = m21;
            this.M22 = m22;
            this.M23 = m23;
            this.M24 = m24;
            this.M31 = m31;
            this.M32 = m32;
            this.M33 = m33;
            this.M34 = m34;
            this.M41 = m41;
            this.M42 = m42;
            this.M43 = m43;
            this.M44 = m44;
        }

        /// <summary>
        /// Returns the identity matrix.
        /// </summary>
        public static float4x4 Identity {
            get { return IdentityValue; }
        }

        /// <summary>
        /// Creates a new viewing <see cref="Matrix"/>.
        /// </summary>
        /// <param name="cameraPosition">Position of the camera.</param>
        /// <param name="cameraTarget">Lookup vector of the camera.</param>
        /// <param name="cameraUpVector">The direction of the upper edge of the camera.</param>
        /// <param name="result">The viewing <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateLookAt(ref float3 cameraPosition, ref float3 cameraTarget, ref float3 cameraUpVector, out float4x4 result) {
            var vector = float3.Normalize(cameraPosition - cameraTarget);
            var vector2 = float3.Normalize(float3.Cross(cameraUpVector, vector));
            var vector3 = float3.Cross(vector, vector2);
            result.M11 = vector2.X;
            result.M12 = vector3.X;
            result.M13 = vector.X;
            result.M14 = 0f;
            result.M21 = vector2.Y;
            result.M22 = vector3.Y;
            result.M23 = vector.Y;
            result.M24 = 0f;
            result.M31 = vector2.Z;
            result.M32 = vector3.Z;
            result.M33 = vector.Z;
            result.M34 = 0f;
            result.M41 = -float3.Dot(vector2, cameraPosition);
            result.M42 = -float3.Dot(vector3, cameraPosition);
            result.M43 = -float3.Dot(vector, cameraPosition);
            result.M44 = 1f;
        }

        /// <summary>
        /// Creates a new projection <see cref="Matrix"/> for customized orthographic view.
        /// </summary>
        /// <param name="left">Lower x-value at the near plane.</param>
        /// <param name="right">Upper x-value at the near plane.</param>
        /// <param name="bottom">Lower y-coordinate at the near plane.</param>
        /// <param name="top">Upper y-value at the near plane.</param>
        /// <param name="zNearPlane">Depth of the near plane.</param>
        /// <param name="zFarPlane">Depth of the far plane.</param>
        /// <param name="result">The new projection <see cref="Matrix"/> for customized orthographic view as an output parameter.</param>
        public static void CreateOrthographicOffCenter(RealEvaluation left, RealEvaluation right, RealEvaluation bottom, RealEvaluation top, RealEvaluation zNearPlane, RealEvaluation zFarPlane, out float4x4 result) {
            result.M11 = (2.0 / ((RealEvaluation)right - (RealEvaluation)left));
            result.M12 = 0.0f;
            result.M13 = 0.0f;
            result.M14 = 0.0f;
            result.M21 = 0.0f;
            result.M22 = (2.0 / ((RealEvaluation)top - (RealEvaluation)bottom));
            result.M23 = 0.0f;
            result.M24 = 0.0f;
            result.M31 = 0.0f;
            result.M32 = 0.0f;
            result.M33 = (1.0 / ((RealEvaluation)zNearPlane - (RealEvaluation)zFarPlane));
            result.M34 = 0.0f;
            result.M41 = (((RealEvaluation)left + (RealEvaluation)right) / ((RealEvaluation)left - (RealEvaluation)right));
            result.M42 = (((RealEvaluation)top + (RealEvaluation)bottom) / ((RealEvaluation)bottom - (RealEvaluation)top));
            result.M43 = ((RealEvaluation)zNearPlane / ((RealEvaluation)zNearPlane - (RealEvaluation)zFarPlane));
            result.M44 = 1.0f;
        }

        /// <summary>
        /// Creates a new projection <see cref="Matrix"/> for perspective view with field of view.
        /// </summary>
        /// <param name="fieldOfView">Field of view in the y direction in radians.</param>
        /// <param name="aspectRatio">Width divided by height of the viewing volume.</param>
        /// <param name="nearPlaneDistance">Distance of the near plane.</param>
        /// <param name="farPlaneDistance">Distance of the far plane, or <see cref="Real.PositiveInfinity"/>.</param>
        /// <param name="result">The new projection <see cref="Matrix"/> for perspective view with FOV as an output parameter.</param>
        public static void CreatePerspectiveFieldOfView(RealEvaluation fieldOfView, RealEvaluation aspectRatio, RealEvaluation nearPlaneDistance, RealEvaluation farPlaneDistance, out float4x4 result) {
            if ((fieldOfView <= 0f) || (fieldOfView >= 3.141593f)) {
                throw new ArgumentException("fieldOfView <= 0 or >= PI");
            }
            if (nearPlaneDistance <= 0f) {
                throw new ArgumentException("nearPlaneDistance <= 0");
            }
            if (farPlaneDistance <= 0f) {
                throw new ArgumentException("farPlaneDistance <= 0");
            }
            if (nearPlaneDistance >= farPlaneDistance) {
                throw new ArgumentException("nearPlaneDistance >= farPlaneDistance");
            }

            var yScale = 1.0f / RealMath.Tan((RealEvaluation)fieldOfView * 0.5f);
            var xScale = yScale / aspectRatio;
            var negFarRange = RealMath.IsPositiveInfinity(farPlaneDistance) ? -1.0f : farPlaneDistance / (nearPlaneDistance - farPlaneDistance);

            result.M11 = xScale;
            result.M12 = result.M13 = result.M14 = 0.0f;
            result.M22 = yScale;
            result.M21 = result.M23 = result.M24 = 0.0f;
            result.M31 = result.M32 = 0.0f;
            result.M33 = negFarRange;
            result.M34 = -1.0f;
            result.M41 = result.M42 = result.M44 = 0.0f;
            result.M43 = nearPlaneDistance * negFarRange;
        }

        /// <summary>
        /// Creates a new scaling <see cref="Matrix"/>.
        /// </summary>
        /// <param name="scale">Scale value for all three axises.</param>
        /// <param name="result">The scaling <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateScale(RealEvaluation scale, out float4x4 result) {
            CreateScale(scale, scale, scale, out result);
        }

        /// <summary>
        /// Creates a new scaling <see cref="Matrix"/>.
        /// </summary>
        /// <param name="xScale">Scale value for X axis.</param>
        /// <param name="yScale">Scale value for Y axis.</param>
        /// <param name="zScale">Scale value for Z axis.</param>
        /// <param name="result">The scaling <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateScale(RealEvaluation xScale, RealEvaluation yScale, RealEvaluation zScale, out float4x4 result) {
            result.M11 = xScale;
            result.M12 = 0;
            result.M13 = 0;
            result.M14 = 0;
            result.M21 = 0;
            result.M22 = yScale;
            result.M23 = 0;
            result.M24 = 0;
            result.M31 = 0;
            result.M32 = 0;
            result.M33 = zScale;
            result.M34 = 0;
            result.M41 = 0;
            result.M42 = 0;
            result.M43 = 0;
            result.M44 = 1;
        }

        /// <summary>
        /// Creates a new translation <see cref="Matrix"/>.
        /// </summary>
        /// <param name="position">X,Y and Z coordinates of translation.</param>
        /// <param name="result">The translation <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateTranslation(RealEvaluation x, RealEvaluation y, RealEvaluation z, out float4x4 result) {
            result.M11 = 1;
            result.M12 = 0;
            result.M13 = 0;
            result.M14 = 0;
            result.M21 = 0;
            result.M22 = 1;
            result.M23 = 0;
            result.M24 = 0;
            result.M31 = 0;
            result.M32 = 0;
            result.M33 = 1;
            result.M34 = 0;
            result.M41 = x;
            result.M42 = y;
            result.M43 = z;
            result.M44 = 1;
        }

        /// <summary>
        /// Creates a new translation <see cref="Matrix"/>.
        /// </summary>
        /// <param name="position">X,Y and Z coordinates of translation.</param>
        /// <param name="result">The translation <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateTranslation(ref float3 position, out float4x4 result) {
            result.M11 = 1;
            result.M12 = 0;
            result.M13 = 0;
            result.M14 = 0;
            result.M21 = 0;
            result.M22 = 1;
            result.M23 = 0;
            result.M24 = 0;
            result.M31 = 0;
            result.M32 = 0;
            result.M33 = 1;
            result.M34 = 0;
            result.M41 = position.X;
            result.M42 = position.Y;
            result.M43 = position.Z;
            result.M44 = 1;
        }

        /// <summary>
        /// Creates a new <see cref="Matrix"/> that contains a multiplication of two matrix.
        /// </summary>
        /// <param name="matrix1">Source <see cref="Matrix"/>.</param>
        /// <param name="matrix2">Source <see cref="Matrix"/>.</param>
        /// <param name="result">Result of the matrix multiplication as an output parameter.</param>
        public static void Multiply(ref float4x4 matrix1, ref float4x4 matrix2, out float4x4 result) {
            var m11 = (((matrix1.M11 * matrix2.M11) + (matrix1.M12 * matrix2.M21)) + (matrix1.M13 * matrix2.M31)) + (matrix1.M14 * matrix2.M41);
            var m12 = (((matrix1.M11 * matrix2.M12) + (matrix1.M12 * matrix2.M22)) + (matrix1.M13 * matrix2.M32)) + (matrix1.M14 * matrix2.M42);
            var m13 = (((matrix1.M11 * matrix2.M13) + (matrix1.M12 * matrix2.M23)) + (matrix1.M13 * matrix2.M33)) + (matrix1.M14 * matrix2.M43);
            var m14 = (((matrix1.M11 * matrix2.M14) + (matrix1.M12 * matrix2.M24)) + (matrix1.M13 * matrix2.M34)) + (matrix1.M14 * matrix2.M44);
            var m21 = (((matrix1.M21 * matrix2.M11) + (matrix1.M22 * matrix2.M21)) + (matrix1.M23 * matrix2.M31)) + (matrix1.M24 * matrix2.M41);
            var m22 = (((matrix1.M21 * matrix2.M12) + (matrix1.M22 * matrix2.M22)) + (matrix1.M23 * matrix2.M32)) + (matrix1.M24 * matrix2.M42);
            var m23 = (((matrix1.M21 * matrix2.M13) + (matrix1.M22 * matrix2.M23)) + (matrix1.M23 * matrix2.M33)) + (matrix1.M24 * matrix2.M43);
            var m24 = (((matrix1.M21 * matrix2.M14) + (matrix1.M22 * matrix2.M24)) + (matrix1.M23 * matrix2.M34)) + (matrix1.M24 * matrix2.M44);
            var m31 = (((matrix1.M31 * matrix2.M11) + (matrix1.M32 * matrix2.M21)) + (matrix1.M33 * matrix2.M31)) + (matrix1.M34 * matrix2.M41);
            var m32 = (((matrix1.M31 * matrix2.M12) + (matrix1.M32 * matrix2.M22)) + (matrix1.M33 * matrix2.M32)) + (matrix1.M34 * matrix2.M42);
            var m33 = (((matrix1.M31 * matrix2.M13) + (matrix1.M32 * matrix2.M23)) + (matrix1.M33 * matrix2.M33)) + (matrix1.M34 * matrix2.M43);
            var m34 = (((matrix1.M31 * matrix2.M14) + (matrix1.M32 * matrix2.M24)) + (matrix1.M33 * matrix2.M34)) + (matrix1.M34 * matrix2.M44);
            var m41 = (((matrix1.M41 * matrix2.M11) + (matrix1.M42 * matrix2.M21)) + (matrix1.M43 * matrix2.M31)) + (matrix1.M44 * matrix2.M41);
            var m42 = (((matrix1.M41 * matrix2.M12) + (matrix1.M42 * matrix2.M22)) + (matrix1.M43 * matrix2.M32)) + (matrix1.M44 * matrix2.M42);
            var m43 = (((matrix1.M41 * matrix2.M13) + (matrix1.M42 * matrix2.M23)) + (matrix1.M43 * matrix2.M33)) + (matrix1.M44 * matrix2.M43);
            var m44 = (((matrix1.M41 * matrix2.M14) + (matrix1.M42 * matrix2.M24)) + (matrix1.M43 * matrix2.M34)) + (matrix1.M44 * matrix2.M44);
            result.M11 = m11;
            result.M12 = m12;
            result.M13 = m13;
            result.M14 = m14;
            result.M21 = m21;
            result.M22 = m22;
            result.M23 = m23;
            result.M24 = m24;
            result.M31 = m31;
            result.M32 = m32;
            result.M33 = m33;
            result.M34 = m34;
            result.M41 = m41;
            result.M42 = m42;
            result.M43 = m43;
            result.M44 = m44;
        }

        /// <summary>
        /// Builds the transposed inverse matrix for normal-vector transformation.
        /// </summary>
        /// <param name="matrix">Matrix to invert and transpose.</param>
        /// <param name="result">Transposed inverse matrix.</param>
        public static void InverseTranspose(ref float4x4 matrix, out float4x4 result) {
            if (!TryInvert(ref matrix, out float4x4 inverted)) {
                throw new InvalidOperationException("Cannot invert a non-invertible transform matrix.");
            }

            Transpose(ref inverted, out result);
        }

        /// <summary>
        /// Attempts to build the transposed inverse matrix for normal-vector transformation, without throwing
        /// for singular matrices such as zero-scale transforms.
        /// </summary>
        /// <param name="matrix">Matrix to invert and transpose.</param>
        /// <param name="result">Transposed inverse matrix, or identity when the matrix is singular.</param>
        /// <returns>True when the matrix was invertible.</returns>
        public static bool TryInverseTranspose(ref float4x4 matrix, out float4x4 result) {
            if (!TryInvert(ref matrix, out float4x4 inverted)) {
                result = Identity;
                return false;
            }

            Transpose(ref inverted, out result);
            return true;
        }

        /// <summary>
        /// Attempts to invert one matrix using scalar cofactors so native runtime draws do not allocate a temporary matrix.
        /// </summary>
        /// <param name="matrix">Matrix to invert.</param>
        /// <param name="result">Inverted matrix when the operation succeeds.</param>
        /// <returns>True when the matrix was invertible; otherwise false.</returns>
        static bool TryInvert(ref float4x4 matrix, out float4x4 result) {
            RealEvaluation m0 = matrix.M11;
            RealEvaluation m1 = matrix.M12;
            RealEvaluation m2 = matrix.M13;
            RealEvaluation m3 = matrix.M14;
            RealEvaluation m4 = matrix.M21;
            RealEvaluation m5 = matrix.M22;
            RealEvaluation m6 = matrix.M23;
            RealEvaluation m7 = matrix.M24;
            RealEvaluation m8 = matrix.M31;
            RealEvaluation m9 = matrix.M32;
            RealEvaluation m10 = matrix.M33;
            RealEvaluation m11 = matrix.M34;
            RealEvaluation m12 = matrix.M41;
            RealEvaluation m13 = matrix.M42;
            RealEvaluation m14 = matrix.M43;
            RealEvaluation m15 = matrix.M44;

            RealEvaluation i0 = m5 * m10 * m15 - m5 * m11 * m14 - m9 * m6 * m15 + m9 * m7 * m14 + m13 * m6 * m11 - m13 * m7 * m10;
            RealEvaluation i4 = -m4 * m10 * m15 + m4 * m11 * m14 + m8 * m6 * m15 - m8 * m7 * m14 - m12 * m6 * m11 + m12 * m7 * m10;
            RealEvaluation i8 = m4 * m9 * m15 - m4 * m11 * m13 - m8 * m5 * m15 + m8 * m7 * m13 + m12 * m5 * m11 - m12 * m7 * m9;
            RealEvaluation i12 = -m4 * m9 * m14 + m4 * m10 * m13 + m8 * m5 * m14 - m8 * m6 * m13 - m12 * m5 * m10 + m12 * m6 * m9;
            RealEvaluation determinant = m0 * i0 + m1 * i4 + m2 * i8 + m3 * i12;

            if (RealMath.Abs(determinant) <= 0.0) {
                result = new float4x4();
                return false;
            }

            RealEvaluation i1 = -m1 * m10 * m15 + m1 * m11 * m14 + m9 * m2 * m15 - m9 * m3 * m14 - m13 * m2 * m11 + m13 * m3 * m10;
            RealEvaluation i5 = m0 * m10 * m15 - m0 * m11 * m14 - m8 * m2 * m15 + m8 * m3 * m14 + m12 * m2 * m11 - m12 * m3 * m10;
            RealEvaluation i9 = -m0 * m9 * m15 + m0 * m11 * m13 + m8 * m1 * m15 - m8 * m3 * m13 - m12 * m1 * m11 + m12 * m3 * m9;
            RealEvaluation i13 = m0 * m9 * m14 - m0 * m10 * m13 - m8 * m1 * m14 + m8 * m2 * m13 + m12 * m1 * m10 - m12 * m2 * m9;

            RealEvaluation i2 = m1 * m6 * m15 - m1 * m7 * m14 - m5 * m2 * m15 + m5 * m3 * m14 + m13 * m2 * m7 - m13 * m3 * m6;
            RealEvaluation i6 = -m0 * m6 * m15 + m0 * m7 * m14 + m4 * m2 * m15 - m4 * m3 * m14 - m12 * m2 * m7 + m12 * m3 * m6;
            RealEvaluation i10 = m0 * m5 * m15 - m0 * m7 * m13 - m4 * m1 * m15 + m4 * m3 * m13 + m12 * m1 * m7 - m12 * m3 * m5;
            RealEvaluation i14 = -m0 * m5 * m14 + m0 * m6 * m13 + m4 * m1 * m14 - m4 * m2 * m13 - m12 * m1 * m6 + m12 * m2 * m5;

            RealEvaluation i3 = -m1 * m6 * m11 + m1 * m7 * m10 + m5 * m2 * m11 - m5 * m3 * m10 - m9 * m2 * m7 + m9 * m3 * m6;
            RealEvaluation i7 = m0 * m6 * m11 - m0 * m7 * m10 - m4 * m2 * m11 + m4 * m3 * m10 + m8 * m2 * m7 - m8 * m3 * m6;
            RealEvaluation i11 = -m0 * m5 * m11 + m0 * m7 * m9 + m4 * m1 * m11 - m4 * m3 * m9 - m8 * m1 * m7 + m8 * m3 * m5;
            RealEvaluation i15 = m0 * m5 * m10 - m0 * m6 * m9 - m4 * m1 * m10 + m4 * m2 * m9 + m8 * m1 * m6 - m8 * m2 * m5;
            RealEvaluation inverseDeterminant = 1.0 / determinant;

            result.M11 = (i0 * inverseDeterminant);
            result.M12 = (i1 * inverseDeterminant);
            result.M13 = (i2 * inverseDeterminant);
            result.M14 = (i3 * inverseDeterminant);
            result.M21 = (i4 * inverseDeterminant);
            result.M22 = (i5 * inverseDeterminant);
            result.M23 = (i6 * inverseDeterminant);
            result.M24 = (i7 * inverseDeterminant);
            result.M31 = (i8 * inverseDeterminant);
            result.M32 = (i9 * inverseDeterminant);
            result.M33 = (i10 * inverseDeterminant);
            result.M34 = (i11 * inverseDeterminant);
            result.M41 = (i12 * inverseDeterminant);
            result.M42 = (i13 * inverseDeterminant);
            result.M43 = (i14 * inverseDeterminant);
            result.M44 = (i15 * inverseDeterminant);
            return true;
        }

        /// <summary>
        /// Transposes the given matrix.
        /// </summary>
        /// <param name="matrix">Matrix to transpose.</param>
        /// <param name="result">Transposed matrix.</param>
        public static void Transpose(ref float4x4 matrix, out float4x4 result) {
            float4x4 ret;

            ret.M11 = matrix.M11;
            ret.M12 = matrix.M21;
            ret.M13 = matrix.M31;
            ret.M14 = matrix.M41;

            ret.M21 = matrix.M12;
            ret.M22 = matrix.M22;
            ret.M23 = matrix.M32;
            ret.M24 = matrix.M42;

            ret.M31 = matrix.M13;
            ret.M32 = matrix.M23;
            ret.M33 = matrix.M33;
            ret.M34 = matrix.M43;

            ret.M41 = matrix.M14;
            ret.M42 = matrix.M24;
            ret.M43 = matrix.M34;
            ret.M44 = matrix.M44;

            result = ret;
        }

        /// <summary>
        /// Creates a rotation matrix from yaw, pitch, and roll angles.
        /// </summary>
        /// <param name="yaw">Yaw around the Y axis in radians.</param>
        /// <param name="pitch">Pitch around the X axis in radians.</param>
        /// <param name="roll">Roll around the Z axis in radians.</param>
        /// <param name="result">Output rotation matrix.</param>
        public static void CreateFromYawPitchRoll(RealEvaluation yaw, RealEvaluation pitch, RealEvaluation roll, out float4x4 result) {
            float4 quaternion;
            float4.CreateFromYawPitchRoll(yaw, pitch, roll, out quaternion);
            CreateFromQuaternion(ref quaternion, out result);
        }

        /// <summary>
        /// Creates a new rotation <see cref="Matrix"/> from a <see cref="Quaternion"/>.
        /// </summary>
        /// <param name="quaternion"><see cref="Quaternion"/> of rotation moment.</param>
        /// <param name="result">The rotation <see cref="Matrix"/> as an output parameter.</param>
        public static void CreateFromQuaternion(ref float4 quaternion, out float4x4 result) {
            RealEvaluation num9 = quaternion.X * quaternion.X;
            RealEvaluation num8 = quaternion.Y * quaternion.Y;
            RealEvaluation num7 = quaternion.Z * quaternion.Z;
            RealEvaluation num6 = quaternion.X * quaternion.Y;
            RealEvaluation num5 = quaternion.Z * quaternion.W;
            RealEvaluation num4 = quaternion.Z * quaternion.X;
            RealEvaluation num3 = quaternion.Y * quaternion.W;
            RealEvaluation num2 = quaternion.Y * quaternion.Z;
            RealEvaluation num = quaternion.X * quaternion.W;
            result.M11 = 1f - (2f * (num8 + num7));
            result.M12 = 2f * (num6 + num5);
            result.M13 = 2f * (num4 - num3);
            result.M14 = 0f;
            result.M21 = 2f * (num6 - num5);
            result.M22 = 1f - (2f * (num7 + num9));
            result.M23 = 2f * (num2 + num);
            result.M24 = 0f;
            result.M31 = 2f * (num4 + num3);
            result.M32 = 2f * (num2 - num);
            result.M33 = 1f - (2f * (num8 + num9));
            result.M34 = 0f;
            result.M41 = 0f;
            result.M42 = 0f;
            result.M43 = 0f;
            result.M44 = 1f;
        }
    }
}
