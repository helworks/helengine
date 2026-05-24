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
        /// BEPU-style contact spring frequency used to keep contact impulses stable without making them rigidly explosive.
        /// </summary>
        const double ContactSpringFrequency = 30d;

        /// <summary>
        /// BEPU-style contact damping ratio used by the default contact material.
        /// </summary>
        const double ContactSpringDampingRatio = 1d;

        /// <summary>
        /// Maximum positive recovery velocity allowed when correcting real penetration.
        /// </summary>
        const double MaximumContactRecoveryVelocity = 2d;

        /// <summary>
        /// Applies one axis-aligned body pair response after the overlap has already been separated.
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

            float3 collisionNormal = CreateAxisNormal(axisIndex, axisDirection);
            if (first.RigidBody.BodyKind == BodyKind3D.Dynamic && second.RigidBody.BodyKind == BodyKind3D.Dynamic) {
                ApplyLinearPairResponse(first, second, collisionNormal);
            } else {
                ApplyPairResponse(first, second, collisionNormal);
            }
        }

        /// <summary>
        /// Applies a warm-started box-box contact constraint after overlap separation has already completed.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Box-box contact manifold describing the shared contact patch.</param>
        /// <param name="constraint">Persistent constraint cache for this body pair.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        public static void ApplyBoxBoxConstraintResponse(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint,
            double stepSeconds) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (constraint == null) {
                throw new ArgumentNullException(nameof(constraint));
            }
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step size must be a finite value greater than zero.");
            }
            if (manifold.ContactCount <= 0) {
                return;
            }

            constraint.WasTouchedThisStep = true;
            constraint.MatchManifold(manifold);
            if (!constraint.WasWarmStartedThisStep) {
                WarmStartBoxBoxConstraint(first, second, manifold, constraint);
                constraint.WasWarmStartedThisStep = true;
            }

            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                SolveBoxBoxPersistentNormalContact(first, second, manifold, constraint, contactIndex, stepSeconds);
            }

            SolveBoxBoxPersistentTangentFriction(first, second, manifold, constraint);
            SolveBoxBoxPersistentTwistFriction(first, second, manifold, constraint);
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
        /// Applies normal and friction response without angular torque for fallback contacts that do not have a reliable contact patch.
        /// </summary>
        /// <param name="first">First body participating in the resolved contact.</param>
        /// <param name="second">Second body participating in the resolved contact.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        static void ApplyLinearPairResponse(BodyState3D first, BodyState3D second, float3 collisionNormal) {
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
            float3 impulse = collisionNormal * (float)normalImpulseMagnitude;
            if (firstInverseMass > 0d) {
                first.Velocity = first.Velocity + (impulse * (float)firstInverseMass);
            }
            if (secondInverseMass > 0d) {
                second.Velocity = second.Velocity + (impulse * (float)(secondInverseMass * -1d));
            }

            ApplyLinearPairFriction(first, second, collisionNormal, normalImpulseMagnitude, firstInverseMass, secondInverseMass);
        }

        /// <summary>
        /// Resolves effective inverse mass for one impulse direction at a contact point.
        /// </summary>
        /// <param name="first">First body participating in the solve.</param>
        /// <param name="second">Second body participating in the solve.</param>
        /// <param name="direction">Unit impulse direction.</param>
        /// <param name="firstOffset">Contact offset from the first body center.</param>
        /// <param name="secondOffset">Contact offset from the second body center.</param>
        /// <returns>Effective inverse mass along the supplied direction.</returns>
        static double ResolveEffectiveMass(BodyState3D first, BodyState3D second, float3 direction, float3 firstOffset, float3 secondOffset) {
            double inverseMass = ResolveInverseMass(first) + ResolveInverseMass(second);
            inverseMass += ResolveAngularEffectiveMass(first, direction, firstOffset);
            inverseMass += ResolveAngularEffectiveMass(second, direction, secondOffset);
            return inverseMass;
        }

        /// <summary>
        /// Resolves one body's angular contribution to effective mass.
        /// </summary>
        /// <param name="bodyState">Body whose inertia should contribute.</param>
        /// <param name="direction">Unit impulse direction.</param>
        /// <param name="offset">Contact offset from the body center.</param>
        /// <returns>Angular effective inverse mass contribution.</returns>
        static double ResolveAngularEffectiveMass(BodyState3D bodyState, float3 direction, float3 offset) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return 0d;
            }

            float3 angular = float3.Cross(offset, direction);
            float3 inertiaAngular = bodyState.ResolveAngularVelocityDelta(angular);
            return float3.Dot(float3.Cross(inertiaAngular, offset), direction);
        }

        /// <summary>
        /// Applies one point impulse to a dynamic body.
        /// </summary>
        /// <param name="bodyState">Body receiving the impulse.</param>
        /// <param name="impulse">World-space impulse.</param>
        /// <param name="offset">Contact offset from the body center.</param>
        static void ApplyPointImpulse(BodyState3D bodyState, float3 impulse, float3 offset) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            float inverseMass = (float)ResolveInverseMass(bodyState);
            bodyState.Velocity = bodyState.Velocity + (impulse * inverseMass);
            float3 angularImpulse = float3.Cross(offset, impulse);
            bodyState.AngularVelocity = bodyState.AngularVelocity + bodyState.ResolveAngularVelocityDelta(angularImpulse);
        }

        /// <summary>
        /// Applies cached impulses from the previous frame before the current sequential solve runs.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        static void WarmStartBoxBoxConstraint(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                float normalImpulse = ResolveNormalImpulse(constraint, contactIndex);
                if (normalImpulse <= 0f) {
                    continue;
                }

                float3 contactPoint = ResolveManifoldContactPoint(manifold, contactIndex);
                float3 impulse = manifold.Normal * normalImpulse;
                ApplyPointImpulse(first, impulse, contactPoint - first.Position);
                ApplyPointImpulse(second, impulse * -1f, contactPoint - second.Position);
            }

            WarmStartBoxBoxTangentFriction(first, second, manifold, constraint);
            WarmStartBoxBoxTwistFriction(first, second, manifold, constraint);
        }

        /// <summary>
        /// Applies cached tangent friction impulse from the previous frame at the current manifold center.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        static void WarmStartBoxBoxTangentFriction(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            if (constraint.TangentImpulse == float3.Zero) {
                return;
            }

            float3 frictionCenter = ResolveManifoldCenter(manifold);
            ApplyPointImpulse(first, constraint.TangentImpulse, frictionCenter - first.Position);
            ApplyPointImpulse(second, constraint.TangentImpulse * -1f, frictionCenter - second.Position);
        }

        /// <summary>
        /// Applies cached twist friction impulse from the previous frame around the current contact normal.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        static void WarmStartBoxBoxTwistFriction(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            if (constraint.TwistImpulse == 0f) {
                return;
            }

            ApplyAngularImpulse(first, manifold.Normal * constraint.TwistImpulse);
            ApplyAngularImpulse(second, manifold.Normal * (constraint.TwistImpulse * -1f));
        }

        /// <summary>
        /// Solves one persistent normal contact and accumulates a nonnegative normal impulse.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        /// <param name="contactIndex">Contact index to solve.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        static void SolveBoxBoxPersistentNormalContact(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint,
            int contactIndex,
            double stepSeconds) {
            float3 contactPoint = ResolveManifoldContactPoint(manifold, contactIndex);
            float3 firstOffset = contactPoint - first.Position;
            float3 secondOffset = contactPoint - second.Position;
            float3 firstVelocity = first.Velocity + float3.Cross(first.AngularVelocity, firstOffset);
            float3 secondVelocity = second.Velocity + float3.Cross(second.AngularVelocity, secondOffset);
            float3 relativeVelocity = firstVelocity - secondVelocity;
            double closingSpeed = float3.Dot(relativeVelocity, manifold.Normal);
            double effectiveMass = ResolveEffectiveMass(first, second, manifold.Normal, firstOffset, secondOffset);
            if (effectiveMass <= 0d) {
                return;
            }

            float previousImpulse = ResolveNormalImpulse(constraint, contactIndex);
            double positionErrorToVelocity = ResolveContactPositionErrorToVelocity(stepSeconds);
            double effectiveMassScale = ResolveContactEffectiveMassScale(stepSeconds);
            double softnessImpulseScale = ResolveContactSoftnessImpulseScale(stepSeconds);
            double softenedEffectiveMass = effectiveMassScale / effectiveMass;
            double biasVelocity = ResolveContactBiasVelocity(ResolveManifoldContactPenetration(manifold, contactIndex), positionErrorToVelocity, stepSeconds);
            float impulseDelta = (float)(-((previousImpulse * softnessImpulseScale) + ((closingSpeed - biasVelocity) * softenedEffectiveMass)));
            float accumulatedImpulse = Math.Max(0f, previousImpulse + impulseDelta);
            float appliedDelta = accumulatedImpulse - previousImpulse;
            if (appliedDelta == 0f) {
                return;
            }

            StoreNormalImpulse(constraint, contactIndex, accumulatedImpulse);
            if (appliedDelta > 0f) {
                StoreFrameNormalImpulse(constraint, contactIndex, ResolveFrameNormalImpulse(constraint, contactIndex) + appliedDelta);
            }
            float3 impulse = manifold.Normal * appliedDelta;
            ApplyPointImpulse(first, impulse, firstOffset);
            ApplyPointImpulse(second, impulse * -1f, secondOffset);
        }

        /// <summary>
        /// Resolves the velocity multiplier used to reduce contact position error over time.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Position-error to target-velocity multiplier.</returns>
        static double ResolveContactPositionErrorToVelocity(double stepSeconds) {
            double angularFrequency = ContactSpringFrequency * Math.PI * 2d;
            return angularFrequency / ((angularFrequency * stepSeconds) + (ContactSpringDampingRatio * 2d));
        }

        /// <summary>
        /// Resolves the BEPU-style effective mass scale contributed by contact softness.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Effective mass scale for the current step.</returns>
        static double ResolveContactEffectiveMassScale(double stepSeconds) {
            double angularFrequency = ContactSpringFrequency * Math.PI * 2d;
            double angularFrequencyStep = angularFrequency * stepSeconds;
            double extra = 1d / (angularFrequencyStep * (angularFrequencyStep + (ContactSpringDampingRatio * 2d)));
            return 1d / (1d + extra);
        }

        /// <summary>
        /// Resolves the accumulated impulse scale that softens the next corrective impulse.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Softness scale applied to the previous accumulated impulse.</returns>
        static double ResolveContactSoftnessImpulseScale(double stepSeconds) {
            double angularFrequency = ContactSpringFrequency * Math.PI * 2d;
            double angularFrequencyStep = angularFrequency * stepSeconds;
            double extra = 1d / (angularFrequencyStep * (angularFrequencyStep + (ContactSpringDampingRatio * 2d)));
            return extra * ResolveContactEffectiveMassScale(stepSeconds);
        }

        /// <summary>
        /// Resolves the target normal velocity from speculative or penetrating contact depth.
        /// </summary>
        /// <param name="penetration">Signed contact penetration depth.</param>
        /// <param name="positionErrorToVelocity">Position-error to target-velocity multiplier.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Bias velocity for the normal constraint.</returns>
        static double ResolveContactBiasVelocity(double penetration, double positionErrorToVelocity, double stepSeconds) {
            if (penetration < 0d) {
                return Math.Max(penetration / stepSeconds, penetration * positionErrorToVelocity);
            }

            return Math.Min(
                penetration / stepSeconds,
                Math.Min(penetration * positionErrorToVelocity, MaximumContactRecoveryVelocity));
        }

        /// <summary>
        /// Solves manifold-level tangent friction using the accumulated normal support impulse as the Coulomb limit.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        static void SolveBoxBoxPersistentTangentFriction(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            float maximumImpulse = ResolveTotalNormalImpulse(constraint) * ResolveManifoldDynamicFriction(first, second, manifold);
            if (maximumImpulse <= 0f) {
                constraint.TangentImpulse = float3.Zero;
                return;
            }

            float3 frictionCenter = ResolveManifoldCenter(manifold);
            float3 firstOffset = frictionCenter - first.Position;
            float3 secondOffset = frictionCenter - second.Position;
            float3 firstVelocity = first.Velocity + float3.Cross(first.AngularVelocity, firstOffset);
            float3 secondVelocity = second.Velocity + float3.Cross(second.AngularVelocity, secondOffset);
            float3 relativeVelocity = firstVelocity - secondVelocity;
            float3 tangentVelocity = relativeVelocity - (manifold.Normal * float3.Dot(relativeVelocity, manifold.Normal));
            double tangentSpeedSquared = float3.Dot(tangentVelocity, tangentVelocity);
            if (tangentSpeedSquared <= 0.0000001d) {
                return;
            }

            float3 tangentDirection = tangentVelocity / (float)Math.Sqrt(tangentSpeedSquared);
            double effectiveMass = ResolveEffectiveMass(first, second, tangentDirection, firstOffset, secondOffset);
            if (effectiveMass <= 0d) {
                return;
            }

            float impulseDelta = (float)(-Math.Sqrt(tangentSpeedSquared) / effectiveMass);
            float3 candidateImpulse = constraint.TangentImpulse + (tangentDirection * impulseDelta);
            float candidateLengthSquared = float3.Dot(candidateImpulse, candidateImpulse);
            float maximumImpulseSquared = maximumImpulse * maximumImpulse;
            if (candidateLengthSquared > maximumImpulseSquared) {
                candidateImpulse = candidateImpulse * (maximumImpulse / (float)Math.Sqrt(candidateLengthSquared));
            }

            float3 appliedImpulse = candidateImpulse - constraint.TangentImpulse;
            constraint.TangentImpulse = candidateImpulse;
            ApplyPointImpulse(first, appliedImpulse, firstOffset);
            ApplyPointImpulse(second, appliedImpulse * -1f, secondOffset);
        }

        /// <summary>
        /// Solves manifold-level twist friction around the shared contact normal.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        static void SolveBoxBoxPersistentTwistFriction(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            float maximumImpulse = ResolveTwistFrictionLimit(first, second, manifold, constraint);
            if (maximumImpulse <= 0f) {
                constraint.TwistImpulse = 0f;
                return;
            }

            double relativeTwistVelocity = float3.Dot(first.AngularVelocity - second.AngularVelocity, manifold.Normal);
            double effectiveMass = ResolveAngularImpulseEffectiveMass(first, second, manifold.Normal);
            if (effectiveMass <= 0d) {
                return;
            }

            float impulseDelta = (float)(-relativeTwistVelocity / effectiveMass);
            float candidateImpulse = Math.Max(-maximumImpulse, Math.Min(maximumImpulse, constraint.TwistImpulse + impulseDelta));
            float appliedImpulse = candidateImpulse - constraint.TwistImpulse;
            if (appliedImpulse == 0f) {
                return;
            }

            constraint.TwistImpulse = candidateImpulse;
            ApplyAngularImpulse(first, manifold.Normal * appliedImpulse);
            ApplyAngularImpulse(second, manifold.Normal * (appliedImpulse * -1f));
        }

        /// <summary>
        /// Applies one angular impulse directly to a dynamic body.
        /// </summary>
        /// <param name="bodyState">Body receiving the impulse.</param>
        /// <param name="angularImpulse">World-space angular impulse.</param>
        static void ApplyAngularImpulse(BodyState3D bodyState, float3 angularImpulse) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            bodyState.AngularVelocity = bodyState.AngularVelocity + bodyState.ResolveAngularVelocityDelta(angularImpulse);
        }

        /// <summary>
        /// Resolves the effective angular inverse mass for a twist impulse around one normal.
        /// </summary>
        /// <param name="first">First body participating in the solve.</param>
        /// <param name="second">Second body participating in the solve.</param>
        /// <param name="normal">Unit angular impulse axis.</param>
        /// <returns>Effective angular inverse mass.</returns>
        static double ResolveAngularImpulseEffectiveMass(BodyState3D first, BodyState3D second, float3 normal) {
            double effectiveMass = 0d;
            if (first.RigidBody.BodyKind == BodyKind3D.Dynamic) {
                effectiveMass += float3.Dot(normal, first.ResolveAngularVelocityDelta(normal));
            }
            if (second.RigidBody.BodyKind == BodyKind3D.Dynamic) {
                effectiveMass += float3.Dot(normal, second.ResolveAngularVelocityDelta(normal));
            }

            return effectiveMass;
        }

        /// <summary>
        /// Resolves the maximum twist impulse from normal support force and contact patch radius.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current contact manifold.</param>
        /// <param name="constraint">Persistent constraint containing cached impulses.</param>
        /// <returns>Maximum twist friction impulse.</returns>
        static float ResolveTwistFrictionLimit(
            BodyState3D first,
            BodyState3D second,
            BoxBoxContactManifold3D manifold,
            BoxBoxContactConstraint3D constraint) {
            float totalNormalImpulse = ResolveTotalNormalImpulse(constraint);
            if (totalNormalImpulse <= 0f) {
                return 0f;
            }

            float3 center = ResolveManifoldCenter(manifold);
            float maximumRadius = 0f;
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                float3 offset = ResolveManifoldContactPoint(manifold, contactIndex) - center;
                maximumRadius = Math.Max(maximumRadius, (float)Math.Sqrt(float3.Dot(offset, offset)));
            }

            return totalNormalImpulse * ResolveManifoldDynamicFriction(first, second, manifold) * maximumRadius;
        }

        /// <summary>
        /// Resolves the effective manifold friction coefficient using BEPU's contact-count scaling for multi-point contact patches.
        /// </summary>
        /// <param name="first">First box body participating in the contact.</param>
        /// <param name="second">Second box body participating in the contact.</param>
        /// <param name="manifold">Current box-box manifold.</param>
        /// <returns>Dynamic friction coefficient scaled for the manifold contact count.</returns>
        static float ResolveManifoldDynamicFriction(BodyState3D first, BodyState3D second, BoxBoxContactManifold3D manifold) {
            if (manifold.ContactCount <= 1) {
                return (float)ResolveCombinedDynamicFriction(first.Collider, second.Collider);
            }

            return (float)ResolveCombinedDynamicFriction(first.Collider, second.Collider) / manifold.ContactCount;
        }

        /// <summary>
        /// Returns the sum of cached normal support impulses.
        /// </summary>
        /// <param name="constraint">Persistent constraint to inspect.</param>
        /// <returns>Total normal impulse accumulated by all contacts.</returns>
        static float ResolveTotalNormalImpulse(BoxBoxContactConstraint3D constraint) {
            float accumulatedTotal = constraint.NormalImpulse0 +
                constraint.NormalImpulse1 +
                constraint.NormalImpulse2 +
                constraint.NormalImpulse3;
            float frameTotal = constraint.FrameNormalImpulse0 +
                constraint.FrameNormalImpulse1 +
                constraint.FrameNormalImpulse2 +
                constraint.FrameNormalImpulse3;
            return Math.Max(accumulatedTotal, frameTotal);
        }

        /// <summary>
        /// Reads one cached normal impulse from a persistent constraint.
        /// </summary>
        /// <param name="constraint">Persistent constraint to inspect.</param>
        /// <param name="contactIndex">Contact index to read.</param>
        /// <returns>Cached normal impulse for the requested contact.</returns>
        static float ResolveNormalImpulse(BoxBoxContactConstraint3D constraint, int contactIndex) {
            if (contactIndex == 0) {
                return constraint.NormalImpulse0;
            }
            if (contactIndex == 1) {
                return constraint.NormalImpulse1;
            }
            if (contactIndex == 2) {
                return constraint.NormalImpulse2;
            }
            if (contactIndex == 3) {
                return constraint.NormalImpulse3;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Reads one positive normal impulse applied during the current step.
        /// </summary>
        /// <param name="constraint">Persistent constraint to inspect.</param>
        /// <param name="contactIndex">Contact index to read.</param>
        /// <returns>Frame normal impulse for the requested contact.</returns>
        static float ResolveFrameNormalImpulse(BoxBoxContactConstraint3D constraint, int contactIndex) {
            if (contactIndex == 0) {
                return constraint.FrameNormalImpulse0;
            }
            if (contactIndex == 1) {
                return constraint.FrameNormalImpulse1;
            }
            if (contactIndex == 2) {
                return constraint.FrameNormalImpulse2;
            }
            if (contactIndex == 3) {
                return constraint.FrameNormalImpulse3;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Stores one cached normal impulse on a persistent constraint.
        /// </summary>
        /// <param name="constraint">Persistent constraint to update.</param>
        /// <param name="contactIndex">Contact index to write.</param>
        /// <param name="impulse">Accumulated impulse to store.</param>
        static void StoreNormalImpulse(BoxBoxContactConstraint3D constraint, int contactIndex, float impulse) {
            if (contactIndex == 0) {
                constraint.NormalImpulse0 = impulse;
                return;
            }
            if (contactIndex == 1) {
                constraint.NormalImpulse1 = impulse;
                return;
            }
            if (contactIndex == 2) {
                constraint.NormalImpulse2 = impulse;
                return;
            }
            if (contactIndex == 3) {
                constraint.NormalImpulse3 = impulse;
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Stores one positive normal impulse applied during the current step.
        /// </summary>
        /// <param name="constraint">Persistent constraint to update.</param>
        /// <param name="contactIndex">Contact index to write.</param>
        /// <param name="impulse">Frame impulse to store.</param>
        static void StoreFrameNormalImpulse(BoxBoxContactConstraint3D constraint, int contactIndex, float impulse) {
            if (contactIndex == 0) {
                constraint.FrameNormalImpulse0 = impulse;
                return;
            }
            if (contactIndex == 1) {
                constraint.FrameNormalImpulse1 = impulse;
                return;
            }
            if (contactIndex == 2) {
                constraint.FrameNormalImpulse2 = impulse;
                return;
            }
            if (contactIndex == 3) {
                constraint.FrameNormalImpulse3 = impulse;
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Resolves one manifold contact point by index.
        /// </summary>
        /// <param name="manifold">Manifold containing the contact point.</param>
        /// <param name="contactIndex">Contact index to read.</param>
        /// <returns>World-space contact point.</returns>
        static float3 ResolveManifoldContactPoint(BoxBoxContactManifold3D manifold, int contactIndex) {
            if (contactIndex == 0) {
                return manifold.Contact0;
            }
            if (contactIndex == 1) {
                return manifold.Contact1;
            }
            if (contactIndex == 2) {
                return manifold.Contact2;
            }
            if (contactIndex == 3) {
                return manifold.Contact3;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Resolves the signed penetration depth for one manifold contact.
        /// </summary>
        /// <param name="manifold">Manifold containing the penetration depths.</param>
        /// <param name="contactIndex">Contact index to read.</param>
        /// <returns>Signed penetration depth for the requested contact.</returns>
        static float ResolveManifoldContactPenetration(BoxBoxContactManifold3D manifold, int contactIndex) {
            if (contactIndex == 0) {
                return manifold.Penetration0;
            }
            if (contactIndex == 1) {
                return manifold.Penetration1;
            }
            if (contactIndex == 2) {
                return manifold.Penetration2;
            }
            if (contactIndex == 3) {
                return manifold.Penetration3;
            }

            throw new ArgumentOutOfRangeException(nameof(contactIndex), "Contact index must be between zero and three.");
        }

        /// <summary>
        /// Resolves the average world-space center of all valid contacts in a manifold.
        /// </summary>
        /// <param name="manifold">Manifold to inspect.</param>
        /// <returns>Average world-space contact center.</returns>
        static float3 ResolveManifoldCenter(BoxBoxContactManifold3D manifold) {
            float3 total = float3.Zero;
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                total = total + ResolveManifoldContactPoint(manifold, contactIndex);
            }

            return total * (1f / manifold.ContactCount);
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
        /// <param name="contactPoint">World-space point where the impulse should apply torque.</param>
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
        /// Applies friction to linear velocity only for contacts whose fallback contact point is not reliable enough for torque.
        /// </summary>
        /// <param name="first">First body participating in the resolved contact.</param>
        /// <param name="second">Second body participating in the resolved contact.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="normalImpulseMagnitude">Positive magnitude of the already-applied normal impulse.</param>
        /// <param name="firstInverseMass">Inverse mass of the first body.</param>
        /// <param name="secondInverseMass">Inverse mass of the second body.</param>
        static void ApplyLinearPairFriction(BodyState3D first, BodyState3D second, float3 collisionNormal, double normalImpulseMagnitude, double firstInverseMass, double secondInverseMass) {
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
                first.Velocity = first.Velocity + (frictionImpulse * (float)(firstInverseMass * -1d));
            }
            if (secondInverseMass > 0d) {
                second.Velocity = second.Velocity + (frictionImpulse * (float)secondInverseMass);
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
