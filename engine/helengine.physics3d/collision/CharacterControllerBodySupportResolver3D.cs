#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_BODY_SUPPORT
namespace helengine {
    /// <summary>
    /// Resolves walkable character-controller support contributed by rigid-body box colliders.
    /// </summary>
    public static class CharacterControllerBodySupportResolver3D {
        /// <summary>
        /// Resolves the highest walkable rigid-body support height beneath the supplied controller footprint.
        /// </summary>
        /// <param name="bodyStates">Runtime rigid bodies that can contribute support.</param>
        /// <param name="centerX">Controller footprint center X coordinate.</param>
        /// <param name="centerY">Controller center Y coordinate.</param>
        /// <param name="centerZ">Controller footprint center Z coordinate.</param>
        /// <param name="halfExtents">Controller half extents.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved highest support height.</param>
        /// <param name="supportBodyState">Resolved support body.</param>
        /// <returns>True when at least one walkable rigid-body support surface was found.</returns>
        public static bool TryResolveSupportHeight(
            IReadOnlyList<BodyState3D> bodyStates,
            float centerX,
            float centerY,
            float centerZ,
            float3 halfExtents,
            double maximumSlopeDegrees,
            out float supportHeight,
            out BodyState3D supportBodyState) {
            if (bodyStates == null) {
                throw new ArgumentNullException(nameof(bodyStates));
            }

            bool foundSupport = false;
            supportHeight = 0f;
            supportBodyState = null;
            float sampleOffsetX = halfExtents.X * 0.8f;
            float sampleOffsetZ = halfExtents.Z * 0.8f;
            float maximumSupportHeight = centerY + halfExtents.Y + 0.05f;

            AccumulateSupportHeight(bodyStates, centerX, centerZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight, ref supportBodyState);
            AccumulateSupportHeight(bodyStates, centerX - sampleOffsetX, centerZ - sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight, ref supportBodyState);
            AccumulateSupportHeight(bodyStates, centerX - sampleOffsetX, centerZ + sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight, ref supportBodyState);
            AccumulateSupportHeight(bodyStates, centerX + sampleOffsetX, centerZ - sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight, ref supportBodyState);
            AccumulateSupportHeight(bodyStates, centerX + sampleOffsetX, centerZ + sampleOffsetZ, maximumSupportHeight, maximumSlopeDegrees, ref foundSupport, ref supportHeight, ref supportBodyState);
            return foundSupport;
        }

        /// <summary>
        /// Resolves the highest support height available at one footprint sample point from rigid-body support surfaces.
        /// </summary>
        /// <param name="bodyStates">Runtime rigid bodies that can contribute support.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSupportHeight">Maximum support height treated as below the controller volume.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="foundSupport">Current support-found flag.</param>
        /// <param name="supportHeight">Current highest support height.</param>
        /// <param name="supportBodyState">Current body that owns the highest support height.</param>
        static void AccumulateSupportHeight(
            IReadOnlyList<BodyState3D> bodyStates,
            float sampleX,
            float sampleZ,
            float maximumSupportHeight,
            double maximumSlopeDegrees,
            ref bool foundSupport,
            ref float supportHeight,
            ref BodyState3D supportBodyState) {
            for (int index = 0; index < bodyStates.Count; index++) {
                BodyState3D bodyState = bodyStates[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Static &&
                    bodyState.RigidBody.BodyKind != BodyKind3D.Kinematic) {
                    continue;
                }
                if (bodyState.Collider.IsTrigger) {
                    continue;
                }

                if (!TryGetSupportHeight(bodyState, sampleX, sampleZ, maximumSlopeDegrees, out float bodySupportHeight)) {
                    continue;
                }
                if (bodySupportHeight > maximumSupportHeight) {
                    continue;
                }

                if (!foundSupport || bodySupportHeight > supportHeight) {
                    foundSupport = true;
                    supportHeight = bodySupportHeight;
                    supportBodyState = bodyState;
                }
            }
        }

        /// <summary>
        /// Resolves the top-face support height contributed by one support box body at the supplied footprint sample point.
        /// </summary>
        /// <param name="bodyState">Support body whose top face should be tested.</param>
        /// <param name="sampleX">Sample X coordinate.</param>
        /// <param name="sampleZ">Sample Z coordinate.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved top-face support height.</param>
        /// <returns>True when the sample point projects onto the body's upward-facing top face.</returns>
        static bool TryGetSupportHeight(BodyState3D bodyState, float sampleX, float sampleZ, double maximumSlopeDegrees, out float supportHeight) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            float3 localAxisX = float4.RotateVector(new float3(1f, 0f, 0f), bodyState.Orientation);
            float3 localAxisY = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            float3 localAxisZ = float4.RotateVector(new float3(0f, 0f, 1f), bodyState.Orientation);
            if (localAxisY.Y <= 0.0001f) {
                supportHeight = 0f;
                return false;
            }
            if (localAxisY.Y < CharacterControllerSupportMath3D.CalculateMinimumWalkableSurfaceDot(maximumSlopeDegrees)) {
                supportHeight = 0f;
                return false;
            }

            float3 planePoint = bodyState.Position + (localAxisY * bodyState.HalfExtents.Y);
            float planeOffset = (localAxisY.X * (sampleX - planePoint.X)) + (localAxisY.Z * (sampleZ - planePoint.Z));
            float sampleY = planePoint.Y - (planeOffset / localAxisY.Y);
            float3 samplePoint = new float3(sampleX, sampleY, sampleZ);
            float3 relativePoint = samplePoint - bodyState.Position;
            float localX = float3.Dot(relativePoint, localAxisX);
            float localY = float3.Dot(relativePoint, localAxisY);
            float localZ = float3.Dot(relativePoint, localAxisZ);
            const float tolerance = 0.05f;
            if (Math.Abs(localX) > bodyState.HalfExtents.X + tolerance) {
                supportHeight = 0f;
                return false;
            }
            if (Math.Abs(localZ) > bodyState.HalfExtents.Z + tolerance) {
                supportHeight = 0f;
                return false;
            }
            if (Math.Abs(localY - bodyState.HalfExtents.Y) > tolerance) {
                supportHeight = 0f;
                return false;
            }

            supportHeight = sampleY;
            return true;
        }
    }
}
#endif
