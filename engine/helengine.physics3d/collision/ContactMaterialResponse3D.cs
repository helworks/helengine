namespace helengine {
    /// <summary>
    /// Applies friction and restitution impulses after one collision resolver has already identified the separation direction.
    /// </summary>
    public static class ContactMaterialResponse3D {
        /// <summary>
        /// Minimum normal impulse magnitude that is treated as intentional contact response instead of floating-point noise.
        /// </summary>
        const double NormalAngularImpulseActivationThreshold = 0.000001d;

        /// <summary>
        /// Minimum box-up alignment that can still use axis-aligned box contact patch math before oriented support points become necessary.
        /// </summary>
        const float AxisAlignedBoxContactUpThreshold = 0.95f;

        /// <summary>
        /// Applies one axis-aligned dynamic body pair response after the overlap has already been separated.
        /// </summary>
        /// <param name="first">First body participating in the resolved contact.</param>
        /// <param name="second">Second body participating in the resolved contact.</param>
        /// <param name="axisIndex">Axis index resolved by the contact finder.</param>
        /// <param name="axisDirection">Sign indicating which side of the axis the first body occupies relative to the second body.</param>
        public static void ApplyAxisPairResponse(BodyState3D first, BodyState3D second, int axisIndex, float axisDirection) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            ApplyPairResponse(first, second, CreateAxisNormal(axisIndex, axisDirection));
        }

        /// <summary>
        /// Applies one normal-based dynamic body pair response after the overlap has already been separated.
        /// </summary>
        /// <param name="first">First body participating in the resolved contact.</param>
        /// <param name="second">Second body participating in the resolved contact.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        public static void ApplyPairResponse(BodyState3D first, BodyState3D second, float3 collisionNormal) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            double firstInverseMass = ResolveInverseMass(first);
            double secondInverseMass = ResolveInverseMass(second);
            double inverseMassSum = firstInverseMass + secondInverseMass;
            if (inverseMassSum <= 0d) {
                return;
            }

            float3 relativeVelocity = first.Velocity - second.Velocity;
            double relativeNormalVelocity = float3.Dot(relativeVelocity, collisionNormal);
            if (relativeNormalVelocity >= 0d) {
                return;
            }

            double restitution = ResolveCombinedRestitution(first.Collider, second.Collider);
            double normalImpulseMagnitude = (-(1d + restitution) * relativeNormalVelocity) / inverseMassSum;
            float3 contactPoint = ResolvePairContactPoint(first, second, collisionNormal, firstInverseMass, secondInverseMass);
            ApplyNormalImpulse(first, second, collisionNormal, normalImpulseMagnitude, firstInverseMass, secondInverseMass, contactPoint);

            ApplyPairFriction(first, second, collisionNormal, normalImpulseMagnitude, firstInverseMass, secondInverseMass, contactPoint);
        }

        /// <summary>
        /// Applies one dynamic-body response against one immovable surface normal after the overlap has already been separated.
        /// </summary>
        /// <param name="bodyState">Dynamic body receiving the response.</param>
        /// <param name="surfaceCollider">Immovable collider providing the contact material values.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the surface and into free space.</param>
        public static void ApplyStaticSurfaceResponse(BodyState3D bodyState, Collider3DComponent surfaceCollider, float3 collisionNormal) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (surfaceCollider == null) {
                throw new ArgumentNullException(nameof(surfaceCollider));
            }

            double normalVelocity = float3.Dot(bodyState.Velocity, collisionNormal);
            if (normalVelocity >= 0d) {
                return;
            }

            double restitution = ResolveCombinedRestitution(bodyState.Collider, surfaceCollider);
            float3 velocityAfterNormalImpulse = bodyState.Velocity - (collisionNormal * (float)((1d + restitution) * normalVelocity));
            double inverseMass = ResolveInverseMass(bodyState);
            float3 contactPoint = bodyState.GetSupportPoint(collisionNormal * -1f);
            float3 normalVelocityDelta = velocityAfterNormalImpulse - bodyState.Velocity;
            double normalImpulseMagnitude = Math.Abs((1d + restitution) * normalVelocity);
            RecordNormalContactLeverArm(bodyState, contactPoint);
            if (ShouldApplyNormalAngularImpulse(normalImpulseMagnitude)) {
                ApplyAngularImpulseFromVelocityDelta(bodyState, normalVelocityDelta, inverseMass, contactPoint);
            }
            float3 velocityAfterFriction = ApplyTangentialFriction(
                velocityAfterNormalImpulse,
                collisionNormal,
                ResolveCombinedStaticFriction(bodyState.Collider, surfaceCollider),
                ResolveCombinedDynamicFriction(bodyState.Collider, surfaceCollider),
                normalImpulseMagnitude);
            float3 frictionVelocityDelta = velocityAfterFriction - velocityAfterNormalImpulse;
            ApplyAngularImpulseFromVelocityDelta(bodyState, frictionVelocityDelta, inverseMass, contactPoint);
            bodyState.Velocity = velocityAfterFriction;
        }

        /// <summary>
        /// Applies one normal impulse to two dynamic bodies according to their inverse masses.
        /// </summary>
        /// <param name="first">First body receiving the impulse in the collision normal direction.</param>
        /// <param name="second">Second body receiving the opposite impulse.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="normalImpulseMagnitude">Positive normal impulse magnitude.</param>
        /// <param name="firstInverseMass">Inverse mass of the first body.</param>
        /// <param name="secondInverseMass">Inverse mass of the second body.</param>
        static void ApplyNormalImpulse(BodyState3D first, BodyState3D second, float3 collisionNormal, double normalImpulseMagnitude, double firstInverseMass, double secondInverseMass, float3 contactPoint) {
            float3 impulse = collisionNormal * (float)normalImpulseMagnitude;
            bool shouldApplyAngularImpulse = ShouldApplyNormalAngularImpulse(normalImpulseMagnitude);
            if (firstInverseMass > 0d) {
                first.Velocity = first.Velocity + (impulse * (float)firstInverseMass);
                RecordNormalContactLeverArm(first, contactPoint);
                if (shouldApplyAngularImpulse) {
                    first.ApplyAngularImpulseAtPoint(impulse, contactPoint);
                }
            }
            if (secondInverseMass > 0d) {
                float3 secondImpulse = impulse * -1f;
                second.Velocity = second.Velocity + (secondImpulse * (float)secondInverseMass);
                RecordNormalContactLeverArm(second, contactPoint);
                if (shouldApplyAngularImpulse) {
                    second.ApplyAngularImpulseAtPoint(secondImpulse, contactPoint);
                }
            }
        }

        /// <summary>
        /// Applies one Coulomb-style tangential friction impulse to two dynamic bodies after their normal impulse has already been resolved.
        /// </summary>
        /// <param name="first">First body participating in the resolved contact.</param>
        /// <param name="second">Second body participating in the resolved contact.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="normalImpulseMagnitude">Positive magnitude of the already-applied normal impulse.</param>
        /// <param name="firstInverseMass">Inverse mass of the first body.</param>
        /// <param name="secondInverseMass">Inverse mass of the second body.</param>
        static void ApplyPairFriction(BodyState3D first, BodyState3D second, float3 collisionNormal, double normalImpulseMagnitude, double firstInverseMass, double secondInverseMass, float3 contactPoint) {
            float3 relativeVelocity = first.Velocity - second.Velocity;
            float3 tangentVelocity = relativeVelocity - (collisionNormal * float3.Dot(relativeVelocity, collisionNormal));
            double tangentSpeedSquared = float3.Dot(tangentVelocity, tangentVelocity);
            if (tangentSpeedSquared <= 0.0000001d) {
                return;
            }

            double tangentSpeed = Math.Sqrt(tangentSpeedSquared);
            float3 tangentDirection = tangentVelocity / (float)tangentSpeed;
            double inverseMassSum = firstInverseMass + secondInverseMass;
            double desiredImpulseMagnitude = tangentSpeed / inverseMassSum;
            double staticLimit = ResolveCombinedStaticFriction(first.Collider, second.Collider) * normalImpulseMagnitude;
            double dynamicLimit = ResolveCombinedDynamicFriction(first.Collider, second.Collider) * normalImpulseMagnitude;
            double frictionImpulseMagnitude = desiredImpulseMagnitude <= staticLimit
                ? desiredImpulseMagnitude
                : Math.Min(dynamicLimit, desiredImpulseMagnitude);

            if (frictionImpulseMagnitude <= 0d) {
                return;
            }

            float3 frictionImpulse = tangentDirection * (float)frictionImpulseMagnitude;
            if (firstInverseMass > 0d) {
                float3 firstImpulse = frictionImpulse * -1f;
                first.Velocity = first.Velocity + (firstImpulse * (float)firstInverseMass);
                first.ApplyAngularImpulseAtPoint(firstImpulse, contactPoint);
            }
            if (secondInverseMass > 0d) {
                second.Velocity = second.Velocity + (frictionImpulse * (float)secondInverseMass);
                second.ApplyAngularImpulseAtPoint(frictionImpulse, contactPoint);
            }
        }

        /// <summary>
        /// Applies the angular part of a linear velocity delta at one contact point.
        /// </summary>
        /// <param name="bodyState">Dynamic body receiving the impulse.</param>
        /// <param name="velocityDelta">Linear velocity delta created by the impulse.</param>
        /// <param name="inverseMass">Inverse mass used by the linear impulse solve.</param>
        /// <param name="contactPoint">World-space contact point used for angular impulse.</param>
        static void ApplyAngularImpulseFromVelocityDelta(BodyState3D bodyState, float3 velocityDelta, double inverseMass, float3 contactPoint) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (inverseMass <= 0d) {
                return;
            }

            float3 impulse = velocityDelta * (float)(1d / inverseMass);
            bodyState.ApplyAngularImpulseAtPoint(impulse, contactPoint);
        }

        /// <summary>
        /// Records the largest horizontal contact lever arm used by normal response during the current step.
        /// </summary>
        /// <param name="bodyState">Dynamic body receiving a normal contact response.</param>
        /// <param name="contactPoint">World-space point where the normal response is applied.</param>
        static void RecordNormalContactLeverArm(BodyState3D bodyState, float3 contactPoint) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            float offsetX = contactPoint.X - bodyState.Position.X;
            float offsetZ = contactPoint.Z - bodyState.Position.Z;
            float leverArm = (float)Math.Sqrt((offsetX * offsetX) + (offsetZ * offsetZ));
            if (leverArm > bodyState.MaximumNormalContactLeverArmXZ) {
                bodyState.MaximumNormalContactLeverArmXZ = leverArm;
            }
        }

        /// <summary>
        /// Applies one static-or-dynamic friction model to a dynamic body that just resolved against an immovable surface.
        /// </summary>
        /// <param name="velocity">Velocity after the normal response has already been applied.</param>
        /// <param name="collisionNormal">Unit normal pointing away from the surface.</param>
        /// <param name="staticFriction">Combined static friction coefficient.</param>
        /// <param name="dynamicFriction">Combined dynamic friction coefficient.</param>
        /// <param name="normalImpulseMagnitude">Approximate magnitude of the already-applied normal impulse.</param>
        /// <returns>Velocity updated with tangential friction.</returns>
        static float3 ApplyTangentialFriction(float3 velocity, float3 collisionNormal, double staticFriction, double dynamicFriction, double normalImpulseMagnitude) {
            float3 tangentialVelocity = velocity - (collisionNormal * float3.Dot(velocity, collisionNormal));
            double tangentSpeedSquared = float3.Dot(tangentialVelocity, tangentialVelocity);
            if (tangentSpeedSquared <= 0.0000001d) {
                return velocity;
            }

            double tangentSpeed = Math.Sqrt(tangentSpeedSquared);
            if (tangentSpeed <= staticFriction * normalImpulseMagnitude) {
                return velocity - tangentialVelocity;
            }

            double reducedSpeed = Math.Max(0d, tangentSpeed - (dynamicFriction * normalImpulseMagnitude));
            return velocity - (tangentialVelocity * (float)((tangentSpeed - reducedSpeed) / tangentSpeed));
        }

        /// <summary>
        /// Resolves one conservative inverse mass for a body state that may or may not be solver-driven.
        /// </summary>
        /// <param name="bodyState">Body whose inverse mass should be resolved.</param>
        /// <returns>Inverse mass used by the collision response.</returns>
        static double ResolveInverseMass(BodyState3D bodyState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return 0d;
            }

            return 1d / bodyState.RigidBody.Mass;
        }

        /// <summary>
        /// Determines whether a normal impulse is large enough to represent an impact instead of a resting support correction.
        /// </summary>
        /// <param name="normalImpulseMagnitude">Positive normal impulse magnitude.</param>
        /// <returns>True when the normal impulse should create angular motion at an off-center contact point.</returns>
        static bool ShouldApplyNormalAngularImpulse(double normalImpulseMagnitude) {
            return normalImpulseMagnitude > NormalAngularImpulseActivationThreshold;
        }

        /// <summary>
        /// Resolves an approximate shared contact point for a pair of primitive bodies.
        /// </summary>
        /// <param name="first">First contact body.</param>
        /// <param name="second">Second contact body.</param>
        /// <param name="collisionNormal">Normal pointing from the second body toward the first body.</param>
        /// <param name="firstInverseMass">Inverse mass of the first body.</param>
        /// <param name="secondInverseMass">Inverse mass of the second body.</param>
        /// <returns>Approximate world-space contact point.</returns>
        static float3 ResolvePairContactPoint(BodyState3D first, BodyState3D second, float3 collisionNormal, double firstInverseMass, double secondInverseMass) {
            if (CanUseAxisAlignedBoxPairContactPoint(first, second)) {
                return ResolveBoxPairContactPoint(first, second, collisionNormal);
            }
            if (firstInverseMass > 0d && secondInverseMass <= 0d) {
                return first.GetSupportPoint(collisionNormal * -1f);
            }
            if (secondInverseMass > 0d && firstInverseMass <= 0d) {
                return second.GetSupportPoint(collisionNormal);
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Box || second.ColliderShapeKind == ColliderShapeKind3D.Box) {
                float3 firstPoint = first.GetSupportPoint(collisionNormal * -1f);
                float3 secondPoint = second.GetSupportPoint(collisionNormal);
                return (firstPoint + secondPoint) * 0.5f;
            }

            int axisIndex = ResolveDominantAxisIndex(collisionNormal);
            float x = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 0);
            float y = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 1);
            float z = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 2);
            return new float3(x, y, z);
        }

        /// <summary>
        /// Resolves one contact point component for a body pair.
        /// </summary>
        /// <param name="first">First contact body.</param>
        /// <param name="second">Second contact body.</param>
        /// <param name="collisionNormal">Normal pointing from the second body toward the first body.</param>
        /// <param name="normalAxisIndex">Dominant normal axis.</param>
        /// <param name="componentIndex">Component index to resolve.</param>
        /// <returns>World-space contact component.</returns>
        static float ResolveContactAxisValue(BodyState3D first, BodyState3D second, float3 collisionNormal, int normalAxisIndex, int componentIndex) {
            if (componentIndex == normalAxisIndex) {
                float direction = GetComponent(collisionNormal, componentIndex);
                float firstCenter = GetComponent(first.Position, componentIndex);
                float firstExtent = GetComponent(first.HalfExtents, componentIndex);
                return direction >= 0f
                    ? firstCenter - firstExtent
                    : firstCenter + firstExtent;
            }

            float firstMinimum = GetComponent(first.Position, componentIndex) - GetComponent(first.HalfExtents, componentIndex);
            float firstMaximum = GetComponent(first.Position, componentIndex) + GetComponent(first.HalfExtents, componentIndex);
            float secondMinimum = GetComponent(second.Position, componentIndex) - GetComponent(second.HalfExtents, componentIndex);
            float secondMaximum = GetComponent(second.Position, componentIndex) + GetComponent(second.HalfExtents, componentIndex);
            float overlapMinimum = Math.Max(firstMinimum, secondMinimum);
            float overlapMaximum = Math.Min(firstMaximum, secondMaximum);
            return (overlapMinimum + overlapMaximum) * 0.5f;
        }

        /// <summary>
        /// Resolves the dominant axis index of one contact normal.
        /// </summary>
        /// <param name="normal">Contact normal to inspect.</param>
        /// <returns>Zero for X, one for Y, or two for Z.</returns>
        static int ResolveDominantAxisIndex(float3 normal) {
            float absoluteX = Math.Abs(normal.X);
            float absoluteY = Math.Abs(normal.Y);
            float absoluteZ = Math.Abs(normal.Z);
            if (absoluteX >= absoluteY && absoluteX >= absoluteZ) {
                return 0;
            }
            if (absoluteY >= absoluteX && absoluteY >= absoluteZ) {
                return 1;
            }

            return 2;
        }

        /// <summary>
        /// Reads one vector component by numeric axis index.
        /// </summary>
        /// <param name="value">Vector to read.</param>
        /// <param name="componentIndex">Zero for X, one for Y, or two for Z.</param>
        /// <returns>Selected vector component.</returns>
        static float GetComponent(float3 value, int componentIndex) {
            if (componentIndex == 0) {
                return value.X;
            }
            if (componentIndex == 1) {
                return value.Y;
            }
            if (componentIndex == 2) {
                return value.Z;
            }

            throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be 0, 1, or 2.");
        }

        /// <summary>
        /// Resolves a contact point at the center of the overlapping box patch so off-center support can create torque.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="collisionNormal">Normal pointing from the second body toward the first body.</param>
        /// <returns>World-space contact point centered on the shared overlap patch.</returns>
        static float3 ResolveBoxPairContactPoint(BodyState3D first, BodyState3D second, float3 collisionNormal) {
            int axisIndex = ResolveDominantAxisIndex(collisionNormal);
            float x = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 0);
            float y = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 1);
            float z = ResolveContactAxisValue(first, second, collisionNormal, axisIndex, 2);
            return new float3(x, y, z);
        }

        /// <summary>
        /// Determines whether both box bodies are upright enough for an axis-aligned overlap patch to represent the contact point.
        /// </summary>
        /// <param name="first">First contact body.</param>
        /// <param name="second">Second contact body.</param>
        /// <returns>True when both bodies are boxes with their local up axes close to world up.</returns>
        static bool CanUseAxisAlignedBoxPairContactPoint(BodyState3D first, BodyState3D second) {
            return first.ColliderShapeKind == ColliderShapeKind3D.Box &&
                second.ColliderShapeKind == ColliderShapeKind3D.Box &&
                IsUprightBox(first) &&
                IsUprightBox(second);
        }

        /// <summary>
        /// Determines whether one box body is close enough to upright for axis-aligned contact-patch math.
        /// </summary>
        /// <param name="bodyState">Body state whose orientation should be inspected.</param>
        /// <returns>True when the box local up axis is nearly aligned with world up.</returns>
        static bool IsUprightBox(BodyState3D bodyState) {
            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            return up.Y >= AxisAlignedBoxContactUpThreshold;
        }

        /// <summary>
        /// Resolves the combined restitution coefficient used by one two-collider contact pair.
        /// </summary>
        /// <param name="first">First collider participating in the contact.</param>
        /// <param name="second">Second collider participating in the contact.</param>
        /// <returns>Combined restitution coefficient.</returns>
        static double ResolveCombinedRestitution(Collider3DComponent first, Collider3DComponent second) {
            return (first.Restitution + second.Restitution) * 0.5d;
        }

        /// <summary>
        /// Resolves the combined static friction coefficient used by one two-collider contact pair.
        /// </summary>
        /// <param name="first">First collider participating in the contact.</param>
        /// <param name="second">Second collider participating in the contact.</param>
        /// <returns>Combined static friction coefficient.</returns>
        static double ResolveCombinedStaticFriction(Collider3DComponent first, Collider3DComponent second) {
            return (first.StaticFriction + second.StaticFriction) * 0.5d;
        }

        /// <summary>
        /// Resolves the combined dynamic friction coefficient used by one two-collider contact pair.
        /// </summary>
        /// <param name="first">First collider participating in the contact.</param>
        /// <param name="second">Second collider participating in the contact.</param>
        /// <returns>Combined dynamic friction coefficient.</returns>
        static double ResolveCombinedDynamicFriction(Collider3DComponent first, Collider3DComponent second) {
            return (first.DynamicFriction + second.DynamicFriction) * 0.5d;
        }

        /// <summary>
        /// Builds one axis normal from a resolved overlap axis and direction sign.
        /// </summary>
        /// <param name="axisIndex">Axis index returned by the primitive contact resolver.</param>
        /// <param name="axisDirection">Sign indicating which side of the axis the first body occupies.</param>
        /// <returns>Unit axis normal pointing from the second body toward the first body.</returns>
        static float3 CreateAxisNormal(int axisIndex, float axisDirection) {
            if (axisIndex == 0) {
                return new float3(axisDirection, 0f, 0f);
            }
            if (axisIndex == 1) {
                return new float3(0f, axisDirection, 0f);
            }
            if (axisIndex == 2) {
                return new float3(0f, 0f, axisDirection);
            }

            throw new ArgumentOutOfRangeException(nameof(axisIndex), "Axis index must be between zero and two.");
        }
    }
}
