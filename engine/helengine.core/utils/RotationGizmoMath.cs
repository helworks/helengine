namespace helengine {
    /// <summary>Solves reusable rotation-gizmo rays, picking and world-axis rotation without owning selection or input capture.</summary>
    public static class RotationGizmoMath {
        /// <summary>Smallest squared magnitude accepted as a direction or quaternion.</summary>
        const double MinimumLengthSquared = 0.000000000001;
        /// <summary>Parallel-intersection tolerance for plane and triangle tests.</summary>
        const double IntersectionTolerance = 0.000000000001;

        /// <summary>Builds a world ray from an explicit viewport, preserving pointer motion beyond its edges during capture.</summary>
        /// <param name="camera">Camera defining the projection and world transform.</param>
        /// <param name="viewport">Window-space rectangle X, Y, width, height; callers may retain it throughout a drag.</param>
        /// <param name="pointer">Unclamped pointer position in the same window coordinates.</param>
        /// <param name="rayOrigin">World origin of the perspective or orthographic ray.</param>
        /// <param name="rayDirection">Normalized world direction.</param>
        /// <returns>False for an unusable viewport or camera projection.</returns>
        public static bool TryBuildCameraRay(CameraComponent camera, float4 viewport, int2 pointer, out float3 rayOrigin, out float3 rayDirection) {
            return TryBuildCameraRay(camera, viewport, pointer, true, out rayOrigin, out rayDirection);
        }

        /// <summary>Builds a camera ray with an explicit choice to honor orthographic state or force perspective compatibility.</summary>
        /// <param name="camera">Camera defining the world transform and projection.</param>
        /// <param name="viewport">Window-space viewport rectangle.</param>
        /// <param name="pointer">Unclamped window-space pointer.</param>
        /// <param name="useCameraProjection">False only for callers explicitly requesting the perspective-only contract.</param>
        /// <param name="rayOrigin">World-space ray origin.</param>
        /// <param name="rayDirection">Unit world-space ray direction.</param>
        /// <returns>Whether finite camera and viewport state produced a ray.</returns>
        public static bool TryBuildCameraRay(CameraComponent camera, float4 viewport, int2 pointer, bool useCameraProjection, out float3 rayOrigin, out float3 rayDirection) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (camera.Parent == null) {
                throw new InvalidOperationException("A gizmo camera must belong to an entity.");
            }
            rayOrigin = float3.Zero;
            rayDirection = float3.Zero;
            if (!IsFinite(viewport) || viewport.Z <= 1 || viewport.W <= 1 || !IsFinite(camera.Parent.Position) || !TryNormalizeOrientation(camera.Parent.Orientation, out float4 orientation)) {
                return false;
            }
            RealEvaluation ndcX = ((pointer.X - viewport.X) / viewport.Z) * 2 - 1;
            RealEvaluation ndcY = 1 - ((pointer.Y - viewport.Y) / viewport.W) * 2;
            RealEvaluation aspect = viewport.Z / viewport.W;
            ICameraProjectionSettings settings = useCameraProjection ? camera as ICameraProjectionSettings : null;
            if (settings != null && settings.ProjectionMode == CameraProjectionMode.Orthographic) {
                RealEvaluation span = settings.OrthographicVerticalSpan;
                if (!RealEvaluation.IsFinite(span) || span < CameraProjectionUtils.MinimumOrthographicVerticalSpan) {
                    return false;
                }
                float3 right = float4.RotateVector(new float3(1, 0, 0), orientation);
                float3 up = float4.RotateVector(new float3(0, 1, 0), orientation);
                float3 origin = camera.Parent.Position + right * (ndcX * span * aspect * 0.5) + up * (ndcY * span * 0.5);
                if (!IsFinite(origin)) {
                    return false;
                }
                rayOrigin = origin;
                return TryNormalize(float4.RotateVector(new float3(0, 0, -1), orientation), out rayDirection);
            }
            if (settings != null && settings.ProjectionMode != CameraProjectionMode.Perspective) {
                return false;
            }
            if (!RealEvaluation.IsFinite(camera.FieldOfView) || camera.FieldOfView <= 0) {
                return false;
            }
            RealEvaluation tangent = RealMath.Tan(CameraProjectionUtils.ClampFieldOfView(camera.FieldOfView) * 0.5);
            float3 localDirection = new float3(ndcX * aspect * tangent, ndcY * tangent, -1);
            if (!TryNormalize(localDirection, out float3 normalizedLocalDirection) || !TryNormalize(float4.RotateVector(normalizedLocalDirection, orientation), out rayDirection)) {
                return false;
            }
            rayOrigin = camera.Parent.Position;
            return true;
        }

        /// <summary>Intersects a forward ray with a rotation plane; parallel, behind-camera and invalid solutions are rejected.</summary>
        /// <param name="rayOrigin">World ray origin.</param>
        /// <param name="rayDirection">Nonzero ray direction; unit length is not required.</param>
        /// <param name="planeOrigin">Fixed drag pivot on the rotation plane.</param>
        /// <param name="planeNormal">Nonzero world rotation axis.</param>
        /// <param name="point">Finite intersection point on success, zero on failure.</param>
        /// <returns>Whether a forward, finite intersection exists.</returns>
        public static bool TryIntersectPlane(float3 rayOrigin, float3 rayDirection, float3 planeOrigin, float3 planeNormal, out float3 point) {
            point = float3.Zero;
            if (!IsFinite(rayOrigin) || !IsFinite(planeOrigin) || !TryNormalize(rayDirection, out float3 direction) || !TryNormalize(planeNormal, out float3 normal)) {
                return false;
            }
            RealEvaluation denominator = float3.Dot(normal, direction);
            if (RealMath.Abs(denominator) <= IntersectionTolerance) {
                return false;
            }
            RealEvaluation distance = float3.Dot(planeOrigin - rayOrigin, normal) / denominator;
            if (!RealEvaluation.IsFinite(distance) || distance < 0) {
                return false;
            }
            float3 intersection = rayOrigin + direction * distance;
            if (!IsFinite(intersection)) {
                return false;
            }
            point = intersection;
            return true;
        }

        /// <summary>Measures the signed principal angle between nonzero drag vectors around a nonzero axis.</summary>
        /// <param name="from">Previous pointer vector from the fixed pivot.</param>
        /// <param name="to">Current pointer vector from the same pivot.</param>
        /// <param name="axis">World rotation axis determining the positive direction.</param>
        /// <returns>Signed radians in the interval from minus pi to pi.</returns>
        public static RealEvaluation ComputeSignedAngle(float3 from, float3 to, float3 axis) {
            float3 normalizedFrom = NormalizeRequired(from, nameof(from));
            float3 normalizedTo = NormalizeRequired(to, nameof(to));
            float3 normalizedAxis = NormalizeRequired(axis, nameof(axis));
            RealEvaluation dot = RealMath.Clamp(float3.Dot(normalizedFrom, normalizedTo), -1, 1);
            return RealMath.Atan2(float3.Dot(float3.Cross(normalizedFrom, normalizedTo), normalizedAxis), dot);
        }

        /// <summary>Applies a world-axis delta to the drag-start quaternion and returns a normalized orientation.</summary>
        /// <param name="startOrientation">Finite nonzero quaternion captured when the drag began.</param>
        /// <param name="worldAxis">Fixed world rotation axis, normalized internally.</param>
        /// <param name="radians">Finite accumulated drag rotation, after any caller-owned snapping.</param>
        /// <returns>Normalized quaternion with world delta concatenated after the starting rotation.</returns>
        public static float4 Rotate(float4 startOrientation, float3 worldAxis, RealEvaluation radians) {
            if (!TryNormalizeOrientation(startOrientation, out float4 start)) {
                throw new ArgumentOutOfRangeException(nameof(startOrientation), "A finite nonzero starting quaternion is required.");
            }
            if (!RealEvaluation.IsFinite(radians)) {
                throw new ArgumentOutOfRangeException(nameof(radians), "The rotation angle must be finite.");
            }
            float3 axis = NormalizeRequired(worldAxis, nameof(worldAxis));
            float4.CreateFromAxisAngle(ref axis, radians, out float4 delta);
            // float4 multiplication is component-wise; concatenation is quaternion composition.
            float4.Concatenate(ref start, ref delta, out float4 result);
            result.Normalize();
            return result;
        }

        /// <summary>Extracts a signed twist angle around the caller's world axis for absolute snap alignment.</summary>
        /// <param name="orientation">Finite nonzero orientation to inspect.</param>
        /// <param name="axis">Nonzero axis around which the twist is measured.</param>
        /// <returns>Signed twist in radians.</returns>
        public static RealEvaluation ResolveAxisTwistAngle(float4 orientation, float3 axis) {
            float3 normal = NormalizeRequired(axis, nameof(axis));
            if (!TryNormalizeOrientation(orientation, out float4 normalized)) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "A finite nonzero quaternion is required.");
            }
            return 2 * RealMath.Atan2(float3.Dot(new float3(normalized.X, normalized.Y, normalized.Z), normal), normalized.W);
        }

        /// <summary>Picks the nearest visible triangle of the three shared tube rings, including their edge-on sidewalls.</summary>
        /// <param name="ring">Local-Y tube-ring mesh used to render each axis.</param>
        /// <param name="rayOrigin">World pointer-ray origin.</param>
        /// <param name="rayDirection">World pointer-ray direction, normalized internally.</param>
        /// <param name="center">Gizmo pivot in world coordinates.</param>
        /// <param name="orientation">Gizmo orientation; identity produces world-axis rings.</param>
        /// <param name="uniformScale">Finite positive rendered gizmo scale.</param>
        /// <param name="axis">Nearest ring index: zero X, one Y, two Z; minus one on a miss.</param>
        /// <param name="distance">Forward world distance to the nearest hit; zero on a miss.</param>
        /// <returns>Whether the rendered ring triangles contain a forward hit.</returns>
        public static bool TryPickAxis(ModelAsset ring, float3 rayOrigin, float3 rayDirection, float3 center, float4 orientation, RealEvaluation uniformScale, out int axis, out RealEvaluation distance) {
            if (ring == null) {
                throw new ArgumentNullException(nameof(ring));
            }
            if (ring.Positions == null || ring.Indices16 == null || ring.Indices16.Length % 3 != 0) {
                throw new ArgumentException("A triangle mesh with positions and 16-bit indices is required.", nameof(ring));
            }
            if (!RealEvaluation.IsFinite(uniformScale) || uniformScale <= 0) {
                throw new ArgumentOutOfRangeException(nameof(uniformScale), "A finite positive gizmo scale is required.");
            }
            axis = -1;
            distance = 0;
            if (!IsFinite(rayOrigin) || !IsFinite(center) || !TryNormalize(rayDirection, out float3 direction) || !TryNormalizeOrientation(orientation, out float4 normalizedOrientation)) {
                return false;
            }
            float4 inverse = new float4(-normalizedOrientation.X, -normalizedOrientation.Y, -normalizedOrientation.Z, normalizedOrientation.W);
            float3 localOrigin = float4.RotateVector(rayOrigin - center, inverse) / uniformScale;
            float3 localDirection = float4.RotateVector(direction, inverse) / uniformScale;
            for (int candidate = 0; candidate < 3; candidate++) {
                float4 axisOrientation = RotationGizmoGeometry.GetAxisOrientation(candidate);
                float4 inverseAxis = new float4(-axisOrientation.X, -axisOrientation.Y, -axisOrientation.Z, axisOrientation.W);
                float3 origin = float4.RotateVector(localOrigin, inverseAxis);
                float3 localRay = float4.RotateVector(localDirection, inverseAxis);
                for (int index = 0; index < ring.Indices16.Length; index += 3) {
                    float3 a = ring.Positions[ring.Indices16[index]];
                    float3 b = ring.Positions[ring.Indices16[index + 1]];
                    float3 c = ring.Positions[ring.Indices16[index + 2]];
                    if (TryIntersectTriangle(origin, localRay, a, b, c, out RealEvaluation candidateDistance) && (axis < 0 || candidateDistance < distance)) {
                        axis = candidate;
                        distance = candidateDistance;
                    }
                }
            }
            return axis >= 0;
        }

        /// <summary>Intersects a ray against either winding of one triangle without changing its distance parameterization.</summary>
        /// <param name="origin">Ray origin in mesh coordinates.</param>
        /// <param name="direction">Ray direction retaining the world-distance scale.</param>
        /// <param name="a">First triangle vertex.</param>
        /// <param name="b">Second triangle vertex.</param>
        /// <param name="c">Third triangle vertex.</param>
        /// <param name="distance">Forward ray distance on success.</param>
        /// <returns>Whether the triangle contains the forward ray intersection.</returns>
        static bool TryIntersectTriangle(float3 origin, float3 direction, float3 a, float3 b, float3 c, out RealEvaluation distance) {
            distance = 0;
            float3 edge1 = b - a;
            float3 edge2 = c - a;
            float3 cross = float3.Cross(direction, edge2);
            RealEvaluation determinant = float3.Dot(edge1, cross);
            if (!RealEvaluation.IsFinite(determinant) || RealMath.Abs(determinant) <= IntersectionTolerance) {
                return false;
            }
            RealEvaluation inverse = 1 / determinant;
            float3 delta = origin - a;
            RealEvaluation u = float3.Dot(delta, cross) * inverse;
            if (u < 0 || u > 1) {
                return false;
            }
            float3 q = float3.Cross(delta, edge1);
            RealEvaluation v = float3.Dot(direction, q) * inverse;
            if (v < 0 || u + v > 1) {
                return false;
            }
            RealEvaluation value = float3.Dot(edge2, q) * inverse;
            if (!RealEvaluation.IsFinite(value) || value < 0) {
                return false;
            }
            distance = value;
            return true;
        }

        /// <summary>Validates and normalizes a required direction instead of manufacturing a fallback axis.</summary>
        /// <param name="value">Direction supplied by the caller.</param>
        /// <param name="parameterName">Parameter name used in validation errors.</param>
        /// <returns>Finite normalized direction.</returns>
        static float3 NormalizeRequired(float3 value, string parameterName) {
            if (!TryNormalize(value, out float3 normalized)) {
                throw new ArgumentOutOfRangeException(parameterName, "A finite nonzero direction is required.");
            }
            return normalized;
        }

        /// <summary>Normalizes one vector while explicitly rejecting zero and nonfinite directions.</summary>
        /// <param name="value">Vector to validate and normalize.</param>
        /// <param name="normalized">Normalized result, or zero on failure.</param>
        /// <returns>Whether the vector has a finite usable magnitude.</returns>
        static bool TryNormalize(float3 value, out float3 normalized) {
            normalized = float3.Zero;
            RealEvaluation lengthSquared = float3.Dot(value, value);
            if (!IsFinite(value) || !RealEvaluation.IsFinite(lengthSquared) || lengthSquared <= MinimumLengthSquared) {
                return false;
            }
            normalized = value / RealMath.Sqrt(lengthSquared);
            return IsFinite(normalized);
        }

        /// <summary>Normalizes a quaternion only when every component and its length are valid.</summary>
        /// <param name="value">Quaternion supplied by a camera or rotation target.</param>
        /// <param name="normalized">Normalized result, or zero on failure.</param>
        /// <returns>Whether a finite nonzero orientation was supplied.</returns>
        static bool TryNormalizeOrientation(float4 value, out float4 normalized) {
            normalized = float4.Zero;
            RealEvaluation lengthSquared = value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W;
            if (!IsFinite(value) || !RealEvaluation.IsFinite(lengthSquared) || lengthSquared <= MinimumLengthSquared) {
                return false;
            }
            RealEvaluation inverseLength = 1 / RealMath.Sqrt(lengthSquared);
            normalized = new float4(value.X * inverseLength, value.Y * inverseLength, value.Z * inverseLength, value.W * inverseLength);
            return IsFinite(normalized);
        }

        /// <summary>Checks every vector component before geometric operations can propagate invalid state.</summary>
        /// <param name="value">Vector to validate.</param>
        /// <returns>Whether all components are finite.</returns>
        static bool IsFinite(float3 value) {
            return RealEvaluation.IsFinite(value.X) && RealEvaluation.IsFinite(value.Y) && RealEvaluation.IsFinite(value.Z);
        }

        /// <summary>Checks every quaternion or viewport component for finite numeric state.</summary>
        /// <param name="value">Quaternion or rectangle to validate.</param>
        /// <returns>Whether all components are finite.</returns>
        static bool IsFinite(float4 value) {
            return RealEvaluation.IsFinite(value.X) && RealEvaluation.IsFinite(value.Y) && RealEvaluation.IsFinite(value.Z) && RealEvaluation.IsFinite(value.W);
        }
    }
}
