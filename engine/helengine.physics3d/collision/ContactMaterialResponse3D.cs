namespace helengine {
    /// <summary>
    /// Applies friction and restitution impulses after one collision resolver has already identified the separation direction.
    /// </summary>
    public static class ContactMaterialResponse3D {
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
            ApplyNormalImpulse(first, second, collisionNormal, normalImpulseMagnitude, firstInverseMass, secondInverseMass);

            ApplyPairFriction(first, second, collisionNormal, normalImpulseMagnitude, firstInverseMass, secondInverseMass);
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
            bodyState.Velocity = ApplyTangentialFriction(
                velocityAfterNormalImpulse,
                collisionNormal,
                ResolveCombinedStaticFriction(bodyState.Collider, surfaceCollider),
                ResolveCombinedDynamicFriction(bodyState.Collider, surfaceCollider),
                Math.Abs((1d + restitution) * normalVelocity));
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
        static void ApplyNormalImpulse(BodyState3D first, BodyState3D second, float3 collisionNormal, double normalImpulseMagnitude, double firstInverseMass, double secondInverseMass) {
            if (firstInverseMass > 0d) {
                first.Velocity = first.Velocity + (collisionNormal * (float)(normalImpulseMagnitude * firstInverseMass));
            }
            if (secondInverseMass > 0d) {
                second.Velocity = second.Velocity - (collisionNormal * (float)(normalImpulseMagnitude * secondInverseMass));
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
        static void ApplyPairFriction(BodyState3D first, BodyState3D second, float3 collisionNormal, double normalImpulseMagnitude, double firstInverseMass, double secondInverseMass) {
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

            if (firstInverseMass > 0d) {
                first.Velocity = first.Velocity - (tangentDirection * (float)(frictionImpulseMagnitude * firstInverseMass));
            }
            if (secondInverseMass > 0d) {
                second.Velocity = second.Velocity + (tangentDirection * (float)(frictionImpulseMagnitude * secondInverseMass));
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
