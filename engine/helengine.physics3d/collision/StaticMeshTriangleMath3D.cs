namespace helengine {
    /// <summary>
    /// Provides shared geometric helpers used by static-mesh interaction resolvers.
    /// </summary>
    public static class StaticMeshTriangleMath3D {
        /// <summary>
        /// Computes the closest point on one triangle to the supplied point.
        /// </summary>
        /// <param name="point">Point being projected.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <returns>Closest point on the triangle.</returns>
        public static float3 GetClosestPointOnTriangle(float3 point, float3 a, float3 b, float3 c) {
            float3 ab = b - a;
            float3 ac = c - a;
            float3 ap = point - a;
            float d1 = float3.Dot(ab, ap);
            float d2 = float3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) {
                return a;
            }

            float3 bp = point - b;
            float d3 = float3.Dot(ab, bp);
            float d4 = float3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) {
                return b;
            }

            float vc = (d1 * d4) - (d3 * d2);
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) {
                float v = d1 / (d1 - d3);
                return a + (ab * v);
            }

            float3 cp = point - c;
            float d5 = float3.Dot(ab, cp);
            float d6 = float3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) {
                return c;
            }

            float vb = (d5 * d2) - (d1 * d6);
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) {
                float w = d2 / (d2 - d6);
                return a + (ac * w);
            }

            float va = (d3 * d6) - (d5 * d4);
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) {
                float3 bc = c - b;
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return b + (bc * w);
            }

            float denominator = 1f / (va + vb + vc);
            float barycentricV = vb * denominator;
            float barycentricW = vc * denominator;
            return a + (ab * barycentricV) + (ac * barycentricW);
        }

        /// <summary>
        /// Computes the unit normal for one triangle, or zero when the triangle is degenerate.
        /// </summary>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <returns>Unit triangle normal or zero when the triangle is degenerate.</returns>
        public static float3 GetTriangleUnitNormal(float3 a, float3 b, float3 c) {
            float3 edgeAB = b - a;
            float3 edgeAC = c - a;
            float3 normal = float3.Cross(edgeAB, edgeAC);
            float normalLengthSquared = float3.Dot(normal, normal);
            if (normalLengthSquared <= 0.0000001f) {
                return float3.Zero;
            }

            float inverseLength = 1f / (float)Math.Sqrt(normalLengthSquared);
            return normal * inverseLength;
        }

        /// <summary>
        /// Determines whether one XZ point lies inside the projected triangle.
        /// </summary>
        /// <param name="point">Projected sample point.</param>
        /// <param name="a">Projected first triangle vertex.</param>
        /// <param name="b">Projected second triangle vertex.</param>
        /// <param name="c">Projected third triangle vertex.</param>
        /// <returns>True when the point lies inside or on the triangle boundary.</returns>
        public static bool IsPointInsideProjectedTriangle(float2 point, float2 a, float2 b, float2 c) {
            float2 edgeAB = Subtract2D(b, a);
            float2 edgeAC = Subtract2D(c, a);
            float area = Cross2D(edgeAB, edgeAC);
            if (Math.Abs(area) <= 0.0000001f) {
                return false;
            }

            float weight0 = Cross2D(Subtract2D(b, point), Subtract2D(c, point)) / area;
            float weight1 = Cross2D(Subtract2D(c, point), Subtract2D(a, point)) / area;
            float weight2 = Cross2D(Subtract2D(a, point), Subtract2D(b, point)) / area;
            const float tolerance = -0.0001f;
            return weight0 >= tolerance && weight1 >= tolerance && weight2 >= tolerance;
        }

        /// <summary>
        /// Clamps one scalar to the supplied inclusive range.
        /// </summary>
        /// <param name="value">Value being clamped.</param>
        /// <param name="minimum">Inclusive minimum value.</param>
        /// <param name="maximum">Inclusive maximum value.</param>
        /// <returns>Clamped scalar value.</returns>
        public static float Clamp(float value, float minimum, float maximum) {
            if (value < minimum) {
                return minimum;
            }
            if (value > maximum) {
                return maximum;
            }

            return value;
        }

        /// <summary>
        /// Subtracts two 2D vectors component-wise.
        /// </summary>
        /// <param name="first">Minuend vector.</param>
        /// <param name="second">Subtrahend vector.</param>
        /// <returns>Component-wise subtraction result.</returns>
        public static float2 Subtract2D(float2 first, float2 second) {
            return new float2(first.X - second.X, first.Y - second.Y);
        }

        /// <summary>
        /// Computes the scalar 2D cross product used by barycentric tests on the XZ plane.
        /// </summary>
        /// <param name="first">First 2D vector.</param>
        /// <param name="second">Second 2D vector.</param>
        /// <returns>Signed scalar cross product.</returns>
        public static float Cross2D(float2 first, float2 second) {
            return (first.X * second.Y) - (first.Y * second.X);
        }
    }
}
