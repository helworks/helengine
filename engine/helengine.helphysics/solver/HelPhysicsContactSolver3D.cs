namespace helengine {
    /// <summary>
    /// Prepares and sequentially solves fixed-capacity scalar contact constraints with warm starting and solved impulse writeback.
    /// </summary>
    sealed class HelPhysicsContactSolver3D {
        /// <summary>
        /// Stores the exact incoming normal-speed boundary below which restitution becomes active.
        /// </summary>
        static readonly PhysicsScalar RestitutionImpactThreshold = PhysicsScalar.FromFloat(-1f);

        /// <summary>
        /// Stores one constructor-owned constraint slot for every contact the solver can process in a step.
        /// </summary>
        readonly HelPhysicsContactConstraint3D[] Constraints;

        /// <summary>
        /// Stores the dedicated split-correction pass that consumes prepared contact pose data.
        /// </summary>
        readonly HelPhysicsPenetrationCorrector3D PenetrationCorrector;

        /// <summary>
        /// Stores how many leading entries of <see cref="Constraints"/> were prepared for the current step.
        /// </summary>
        int ConstraintCount;

        /// <summary>
        /// Initializes fixed contact-constraint storage for the lifetime of this solver.
        /// </summary>
        /// <param name="contactCapacity">Positive maximum number of contacts that one step may prepare.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contactCapacity"/> is not positive.</exception>
        public HelPhysicsContactSolver3D(int contactCapacity) {
            if (contactCapacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(contactCapacity), "Contact solver capacity must be positive.");
            }

            Constraints = new HelPhysicsContactConstraint3D[contactCapacity];
            PenetrationCorrector = new HelPhysicsPenetrationCorrector3D();
        }

        /// <summary>
        /// Builds one fixed solver constraint per current manifold contact in deterministic manifold and contact order.
        /// </summary>
        /// <param name="stepSeconds">Positive simulation duration associated with the prepared constraints.</param>
        /// <param name="bodies">Fixed body pool addressed by the parallel pair keys.</param>
        /// <param name="pairs">Canonical body pairs parallel to <paramref name="manifolds"/>.</param>
        /// <param name="manifolds">Current manifolds whose contacts and cached impulses are prepared.</param>
        /// <param name="manifoldCount">Number of leading parallel pair and manifold entries to prepare.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the step or manifold count is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required pool or array is <see langword="null"/>.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when current contacts exceed constructor-owned constraint storage.</exception>
        public void Prepare(
            PhysicsScalar stepSeconds,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            if (stepSeconds <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Contact preparation requires a positive simulation step.");
            }

            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            } else if (pairs == null) {
                throw new ArgumentNullException(nameof(pairs));
            } else if (manifolds == null) {
                throw new ArgumentNullException(nameof(manifolds));
            }

            if (manifoldCount < 0 || manifoldCount > pairs.Length || manifoldCount > manifolds.Length) {
                throw new ArgumentOutOfRangeException(nameof(manifoldCount), "Manifold count must fit both parallel input arrays.");
            }

            int requiredConstraintCount = 0;
            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                int contactCount = manifolds[manifoldIndex].ContactCount;
                if (contactCount < 0 || contactCount > 4) {
                    throw new InvalidOperationException("Contact manifolds must contain between zero and four contacts.");
                }

                requiredConstraintCount += contactCount;
            }

            if (requiredConstraintCount > Constraints.Length) {
                throw new HelPhysicsCapacityExceededException("solver constraint", Constraints.Length);
            }

            ConstraintCount = 0;
            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(pair.SecondBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateA = ref bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateB = ref bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex);
                bool respondsA = coldStateA.BodyKind == BodyKind3D.Dynamic && bodyA.IsAwake;
                bool respondsB = coldStateB.BodyKind == BodyKind3D.Dynamic && bodyB.IsAwake;
                PhysicsScalar inverseMassA = respondsA ? bodyA.InverseMass : PhysicsScalar.Zero;
                PhysicsScalar inverseMassB = respondsB ? bodyB.InverseMass : PhysicsScalar.Zero;
                PhysicsMatrix3x3 worldInverseInertiaA = default;
                PhysicsMatrix3x3 worldInverseInertiaB = default;
                if (respondsA) {
                    PhysicsMatrix3x3 rotationA = PhysicsMatrix3x3.CreateFromQuaternion(bodyA.Orientation);
                    worldInverseInertiaA = rotationA * bodyA.LocalInverseInertia * rotationA.Transposed();
                }
                if (respondsB) {
                    PhysicsMatrix3x3 rotationB = PhysicsMatrix3x3.CreateFromQuaternion(bodyB.Orientation);
                    worldInverseInertiaB = rotationB * bodyB.LocalInverseInertia * rotationB.Transposed();
                }

                PhysicsScalar staticFriction = PhysicsScalar.Sqrt(
                    coldStateA.Material.StaticFriction * coldStateB.Material.StaticFriction);
                PhysicsScalar dynamicFriction = PhysicsScalar.Sqrt(
                    coldStateA.Material.DynamicFriction * coldStateB.Material.DynamicFriction);
                PhysicsScalar restitution = PhysicsScalar.Max(
                    coldStateA.Material.Restitution,
                    coldStateB.Material.Restitution);

                HelPhysicsContactManifold3D manifold = manifolds[manifoldIndex];
                for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                    HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                    ref HelPhysicsContactConstraint3D constraint = ref Constraints[ConstraintCount++];
                    constraint.BodyAIndex = pair.FirstBodyIndex;
                    constraint.BodyBIndex = pair.SecondBodyIndex;
                    constraint.ManifoldIndex = manifoldIndex;
                    constraint.ContactIndex = contactIndex;
                    constraint.Normal = contact.Normal;
                    constraint.Tangent0 = CreateFirstTangent(contact.Normal);
                    constraint.Tangent1 = PhysicsVector3.Cross(contact.Normal, constraint.Tangent0);
                    constraint.LeverArmA = bodyA.Orientation.Rotate(contact.LocalAnchorA);
                    constraint.LeverArmB = bodyB.Orientation.Rotate(contact.LocalAnchorB);
                    constraint.WorldInverseInertiaA = worldInverseInertiaA;
                    constraint.WorldInverseInertiaB = worldInverseInertiaB;
                    constraint.InverseMassA = inverseMassA;
                    constraint.InverseMassB = inverseMassB;
                    constraint.NormalEffectiveMass = ComputeEffectiveMass(
                        constraint.Normal,
                        constraint.LeverArmA,
                        constraint.LeverArmB,
                        inverseMassA,
                        inverseMassB,
                        worldInverseInertiaA,
                        worldInverseInertiaB);
                    constraint.TangentEffectiveMass0 = ComputeEffectiveMass(
                        constraint.Tangent0,
                        constraint.LeverArmA,
                        constraint.LeverArmB,
                        inverseMassA,
                        inverseMassB,
                        worldInverseInertiaA,
                        worldInverseInertiaB);
                    constraint.TangentEffectiveMass1 = ComputeEffectiveMass(
                        constraint.Tangent1,
                        constraint.LeverArmA,
                        constraint.LeverArmB,
                        inverseMassA,
                        inverseMassB,
                        worldInverseInertiaA,
                        worldInverseInertiaB);
                    PhysicsVector3 relativeVelocity = ComputeRelativeVelocity(
                        in bodyA,
                        in bodyB,
                        constraint.LeverArmA,
                        constraint.LeverArmB);
                    PhysicsScalar incomingNormalVelocity = PhysicsVector3.Dot(relativeVelocity, constraint.Normal);
                    constraint.RestitutionVelocity = PhysicsScalar.Zero;
                    if (incomingNormalVelocity < RestitutionImpactThreshold) {
                        constraint.RestitutionVelocity = -(restitution * incomingNormalVelocity);
                    }
                    constraint.StaticFriction = staticFriction;
                    constraint.DynamicFriction = dynamicFriction;
                    constraint.PenetrationDepth = contact.PenetrationDepth;
                    constraint.AccumulatedNormalImpulse = contact.AccumulatedNormalImpulse;
                    constraint.AccumulatedTangentImpulse0 = contact.AccumulatedTangentImpulse0;
                    constraint.AccumulatedTangentImpulse1 = contact.AccumulatedTangentImpulse1;
                    constraint.RespondsA = respondsA;
                    constraint.RespondsB = respondsB;
                }
            }
        }

        /// <summary>
        /// Applies every prepared contact's cached normal and tangent impulses to its responsive bodies.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void WarmStart(HelPhysicsBodyPool3D bodies) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                PhysicsVector3 impulse =
                    (constraint.Normal * constraint.AccumulatedNormalImpulse) +
                    (constraint.Tangent0 * constraint.AccumulatedTangentImpulse0) +
                    (constraint.Tangent1 * constraint.AccumulatedTangentImpulse1);
                ApplyImpulse(ref constraint, impulse, bodies);
            }
        }

        /// <summary>
        /// Runs one deterministic sequential normal-and-friction impulse iteration over all prepared contacts.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void SolveVelocityIteration(HelPhysicsBodyPool3D bodies) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
                PhysicsVector3 relativeVelocity = ComputeRelativeVelocity(
                    in bodyA,
                    in bodyB,
                    constraint.LeverArmA,
                    constraint.LeverArmB);
                PhysicsScalar normalVelocity = PhysicsVector3.Dot(relativeVelocity, constraint.Normal);
                PhysicsScalar normalImpulseDelta =
                    (constraint.RestitutionVelocity - normalVelocity) * constraint.NormalEffectiveMass;
                PhysicsScalar oldNormalImpulse = constraint.AccumulatedNormalImpulse;
                constraint.AccumulatedNormalImpulse = PhysicsScalar.Max(
                    oldNormalImpulse + normalImpulseDelta,
                    PhysicsScalar.Zero);
                normalImpulseDelta = constraint.AccumulatedNormalImpulse - oldNormalImpulse;
                ApplyImpulse(ref constraint, constraint.Normal * normalImpulseDelta, bodies);

                relativeVelocity = ComputeRelativeVelocity(
                    in bodyA,
                    in bodyB,
                    constraint.LeverArmA,
                    constraint.LeverArmB);
                PhysicsScalar candidateTangentImpulse0 =
                    constraint.AccumulatedTangentImpulse0 -
                    (PhysicsVector3.Dot(relativeVelocity, constraint.Tangent0) * constraint.TangentEffectiveMass0);
                PhysicsScalar candidateTangentImpulse1 =
                    constraint.AccumulatedTangentImpulse1 -
                    (PhysicsVector3.Dot(relativeVelocity, constraint.Tangent1) * constraint.TangentEffectiveMass1);
                PhysicsScalar staticLimit = constraint.StaticFriction * constraint.AccumulatedNormalImpulse;
                PhysicsScalar candidateLengthSquared =
                    (candidateTangentImpulse0 * candidateTangentImpulse0) +
                    (candidateTangentImpulse1 * candidateTangentImpulse1);
                PhysicsScalar newTangentImpulse0;
                PhysicsScalar newTangentImpulse1;
                if (candidateLengthSquared <= staticLimit * staticLimit) {
                    newTangentImpulse0 = candidateTangentImpulse0;
                    newTangentImpulse1 = candidateTangentImpulse1;
                } else {
                    PhysicsScalar dynamicLimit = constraint.DynamicFriction * constraint.AccumulatedNormalImpulse;
                    if (candidateLengthSquared > PhysicsScalar.Zero && dynamicLimit > PhysicsScalar.Zero) {
                        PhysicsScalar dynamicScale = dynamicLimit * PhysicsScalar.ReciprocalSqrt(candidateLengthSquared);
                        newTangentImpulse0 = candidateTangentImpulse0 * dynamicScale;
                        newTangentImpulse1 = candidateTangentImpulse1 * dynamicScale;
                    } else {
                        newTangentImpulse0 = PhysicsScalar.Zero;
                        newTangentImpulse1 = PhysicsScalar.Zero;
                    }
                }

                PhysicsScalar tangentImpulseDelta0 = newTangentImpulse0 - constraint.AccumulatedTangentImpulse0;
                PhysicsScalar tangentImpulseDelta1 = newTangentImpulse1 - constraint.AccumulatedTangentImpulse1;
                constraint.AccumulatedTangentImpulse0 = newTangentImpulse0;
                constraint.AccumulatedTangentImpulse1 = newTangentImpulse1;
                PhysicsVector3 tangentImpulse =
                    (constraint.Tangent0 * tangentImpulseDelta0) +
                    (constraint.Tangent1 * tangentImpulseDelta1);
                ApplyImpulse(ref constraint, tangentImpulse, bodies);
            }
        }

        /// <summary>
        /// Copies final accumulated normal and tangent impulses back to each prepared current manifold contact.
        /// </summary>
        /// <param name="manifolds">Current manifold array originally supplied to <see cref="Prepare"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manifolds"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when prepared manifold/contact layout changed before writeback.</exception>
        public void WriteBack(HelPhysicsContactManifold3D[] manifolds) {
            if (manifolds == null) {
                throw new ArgumentNullException(nameof(manifolds));
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                if (constraint.ManifoldIndex >= manifolds.Length ||
                    constraint.ContactIndex >= manifolds[constraint.ManifoldIndex].ContactCount) {
                    throw new InvalidOperationException("Prepared manifold contacts must remain present through solved impulse writeback.");
                }

                HelPhysicsContactPoint3D contact =
                    manifolds[constraint.ManifoldIndex].GetContact(constraint.ContactIndex);
                contact.AccumulatedNormalImpulse = constraint.AccumulatedNormalImpulse;
                contact.AccumulatedTangentImpulse0 = constraint.AccumulatedTangentImpulse0;
                contact.AccumulatedTangentImpulse1 = constraint.AccumulatedTangentImpulse1;
                manifolds[constraint.ManifoldIndex].SetContact(constraint.ContactIndex, in contact);
            }
        }

        /// <summary>
        /// Corrects prepared penetration through a pose-only pass that leaves velocities and accumulated impulses unchanged.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void CorrectPenetration(HelPhysicsBodyPool3D bodies) {
            PenetrationCorrector.CorrectPenetration(bodies, Constraints, ConstraintCount);
        }

        /// <summary>
        /// Creates a deterministic, numerically stable first tangent by crossing the normal with its least-aligned cardinal axis.
        /// </summary>
        /// <param name="normal">Unit contact normal requiring a perpendicular basis.</param>
        /// <returns>Normalized first tangent with deterministic sign and axis selection.</returns>
        static PhysicsVector3 CreateFirstTangent(PhysicsVector3 normal) {
            PhysicsScalar absoluteX = PhysicsScalar.Abs(normal.X);
            PhysicsScalar absoluteY = PhysicsScalar.Abs(normal.Y);
            PhysicsScalar absoluteZ = PhysicsScalar.Abs(normal.Z);
            if (absoluteX <= absoluteY && absoluteX <= absoluteZ) {
                return PhysicsVector3.Cross(PhysicsVector3.UnitX, normal).Normalized();
            } else if (absoluteY <= absoluteZ) {
                return PhysicsVector3.Cross(PhysicsVector3.UnitY, normal).Normalized();
            }

            return PhysicsVector3.Cross(PhysicsVector3.UnitZ, normal).Normalized();
        }

        /// <summary>
        /// Computes reciprocal scalar effective mass along one world-space constraint direction.
        /// </summary>
        /// <param name="direction">Unit normal or tangent constraint direction.</param>
        /// <param name="leverArmA">World-space body A contact lever arm.</param>
        /// <param name="leverArmB">World-space body B contact lever arm.</param>
        /// <param name="inverseMassA">Responsive inverse mass for body A.</param>
        /// <param name="inverseMassB">Responsive inverse mass for body B.</param>
        /// <param name="worldInverseInertiaA">Responsive world inverse inertia for body A.</param>
        /// <param name="worldInverseInertiaB">Responsive world inverse inertia for body B.</param>
        /// <returns>Reciprocal effective mass, or zero when neither body can respond along the direction.</returns>
        static PhysicsScalar ComputeEffectiveMass(
            PhysicsVector3 direction,
            PhysicsVector3 leverArmA,
            PhysicsVector3 leverArmB,
            PhysicsScalar inverseMassA,
            PhysicsScalar inverseMassB,
            PhysicsMatrix3x3 worldInverseInertiaA,
            PhysicsMatrix3x3 worldInverseInertiaB) {
            PhysicsVector3 angularDirectionA = PhysicsVector3.Cross(leverArmA, direction);
            PhysicsVector3 angularDirectionB = PhysicsVector3.Cross(leverArmB, direction);
            PhysicsScalar denominator =
                inverseMassA +
                inverseMassB +
                PhysicsVector3.Dot(angularDirectionA, worldInverseInertiaA.Transform(angularDirectionA)) +
                PhysicsVector3.Dot(angularDirectionB, worldInverseInertiaB.Transform(angularDirectionB));
            if (denominator <= PhysicsScalar.Zero) {
                return PhysicsScalar.Zero;
            }

            return PhysicsScalar.One / denominator;
        }

        /// <summary>
        /// Computes body B contact-point velocity relative to body A from current linear and angular velocity.
        /// </summary>
        /// <param name="bodyA">Current body A hot state.</param>
        /// <param name="bodyB">Current body B hot state.</param>
        /// <param name="leverArmA">World-space body A contact lever arm.</param>
        /// <param name="leverArmB">World-space body B contact lever arm.</param>
        /// <returns>World-space relative contact velocity directed from body A motion to body B motion.</returns>
        static PhysicsVector3 ComputeRelativeVelocity(
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBodyState3D bodyB,
            PhysicsVector3 leverArmA,
            PhysicsVector3 leverArmB) {
            PhysicsVector3 velocityA = bodyA.LinearVelocity + PhysicsVector3.Cross(bodyA.AngularVelocity, leverArmA);
            PhysicsVector3 velocityB = bodyB.LinearVelocity + PhysicsVector3.Cross(bodyB.AngularVelocity, leverArmB);
            return velocityB - velocityA;
        }

        /// <summary>
        /// Applies one world-space contact impulse with opposite signs to responsive body A and body B state.
        /// </summary>
        /// <param name="constraint">Prepared contact containing response flags, masses, inertias, and lever arms.</param>
        /// <param name="impulse">World-space impulse directed from body A toward body B.</param>
        /// <param name="bodies">Fixed body pool containing both prepared participants.</param>
        static void ApplyImpulse(
            ref HelPhysicsContactConstraint3D constraint,
            PhysicsVector3 impulse,
            HelPhysicsBodyPool3D bodies) {
            if (constraint.RespondsA) {
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
                bodyA.LinearVelocity -= impulse * constraint.InverseMassA;
                bodyA.AngularVelocity -= constraint.WorldInverseInertiaA.Transform(
                    PhysicsVector3.Cross(constraint.LeverArmA, impulse));
            }

            if (constraint.RespondsB) {
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
                bodyB.LinearVelocity += impulse * constraint.InverseMassB;
                bodyB.AngularVelocity += constraint.WorldInverseInertiaB.Transform(
                    PhysicsVector3.Cross(constraint.LeverArmB, impulse));
            }
        }
    }
}
