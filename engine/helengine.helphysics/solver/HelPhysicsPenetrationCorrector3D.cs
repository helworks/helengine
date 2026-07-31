namespace helengine {
    /// <summary>
    /// Applies split positional contact correction to body poses without changing kinetic velocities or solver impulses.
    /// </summary>
    sealed class HelPhysicsPenetrationCorrector3D {
        /// <summary>
        /// Stores overlap tolerated before positional correction begins.
        /// </summary>
        static readonly PhysicsScalar PenetrationSlop = PhysicsScalar.FromFloat(0.005f);

        /// <summary>
        /// Stores the fraction of penetration beyond slop removed by one correction pass.
        /// </summary>
        static readonly PhysicsScalar CorrectionFraction = PhysicsScalar.FromFloat(0.2f);

        /// <summary>
        /// Stores the largest separation distance one contact may request in one correction pass.
        /// </summary>
        static readonly PhysicsScalar MaximumCorrection = PhysicsScalar.FromFloat(0.2f);

        /// <summary>
        /// Corrects all prepared contact poses from one pass-wide snapshot, rebuilding later-pass geometry before deterministic impulse application.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared contact participant.</param>
        /// <param name="constraints">Constructor-owned solver constraints containing penetration and response data.</param>
        /// <param name="constraintCount">Number of leading prepared constraints to correct.</param>
        /// <param name="refreshGeometry">Whether an earlier pass changed poses after initial narrowphase preparation.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required pool or constraint array is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="constraintCount"/> lies outside the fixed array.</exception>
        public void CorrectPenetration(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsContactConstraint3D[] constraints,
            int constraintCount,
            bool refreshGeometry) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            } else if (constraints == null) {
                throw new ArgumentNullException(nameof(constraints));
            }

            if (constraintCount < 0 || constraintCount > constraints.Length) {
                throw new ArgumentOutOfRangeException(nameof(constraintCount), "Correction count must fit fixed constraint storage.");
            }

            if (refreshGeometry) {
                for (int constraintIndex = 0; constraintIndex < constraintCount; constraintIndex++) {
                    RefreshCorrectionGeometry(ref constraints[constraintIndex], bodies);
                }
            }

            for (int constraintIndex = 0; constraintIndex < constraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref constraints[constraintIndex];
                PhysicsScalar correctableDepth = constraint.PenetrationDepth - PenetrationSlop;
                if (correctableDepth <= PhysicsScalar.Zero || constraint.NormalEffectiveMass <= PhysicsScalar.Zero) {
                    continue;
                }

                PhysicsScalar correctionDistance = PhysicsScalar.Min(
                    correctableDepth * CorrectionFraction,
                    MaximumCorrection);
                PhysicsScalar positionalImpulseMagnitude = correctionDistance * constraint.NormalEffectiveMass;
                PhysicsVector3 positionalImpulse = constraint.Normal * positionalImpulseMagnitude;
                if (constraint.RespondsA) {
                    ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
                    bodyA.Position -= positionalImpulse * constraint.InverseMassA;
                    PhysicsVector3 angularCorrectionA = -constraint.WorldInverseInertiaA.Transform(
                        PhysicsVector3.Cross(constraint.LeverArmA, positionalImpulse));
                    bodyA.Orientation = ApplyAngularCorrection(bodyA.Orientation, angularCorrectionA);
                }

                if (constraint.RespondsB) {
                    ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
                    bodyB.Position += positionalImpulse * constraint.InverseMassB;
                    PhysicsVector3 angularCorrectionB = constraint.WorldInverseInertiaB.Transform(
                        PhysicsVector3.Cross(constraint.LeverArmB, positionalImpulse));
                    bodyB.Orientation = ApplyAngularCorrection(bodyB.Orientation, angularCorrectionB);
                }
            }
        }

        /// <summary>
        /// Rebuilds one prepared contact's pose-dependent geometry and scalar response from the bodies' current corrected poses.
        /// </summary>
        /// <param name="constraint">Prepared contact whose local anchors and response flags remain valid across passes.</param>
        /// <param name="bodies">Fixed body pool containing both current participant poses and local inertia.</param>
        static void RefreshCorrectionGeometry(
            ref HelPhysicsContactConstraint3D constraint,
            HelPhysicsBodyPool3D bodies) {
            ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
            ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
            constraint.LeverArmA = bodyA.Orientation.Rotate(constraint.LocalAnchorA);
            constraint.LeverArmB = bodyB.Orientation.Rotate(constraint.LocalAnchorB);
            PhysicsVector3 worldAnchorA = bodyA.Position + constraint.LeverArmA;
            PhysicsVector3 worldAnchorB = bodyB.Position + constraint.LeverArmB;
            PhysicsScalar separation = PhysicsVector3.Dot(
                worldAnchorB - worldAnchorA,
                constraint.Normal);
            constraint.PenetrationDepth = PhysicsScalar.Max(-separation, PhysicsScalar.Zero);
            constraint.WorldInverseInertiaA = CreateWorldInverseInertia(in bodyA, constraint.RespondsA);
            constraint.WorldInverseInertiaB = CreateWorldInverseInertia(in bodyB, constraint.RespondsB);
            constraint.NormalEffectiveMass = ComputeEffectiveMass(
                constraint.Normal,
                constraint.LeverArmA,
                constraint.LeverArmB,
                constraint.InverseMassA,
                constraint.InverseMassB,
                constraint.WorldInverseInertiaA,
                constraint.WorldInverseInertiaB);
        }

        /// <summary>
        /// Builds orientation-derived world inverse inertia for a responsive body and exact zero response otherwise.
        /// </summary>
        /// <param name="body">Current body pose and local inverse inertia.</param>
        /// <param name="responds">Whether positional impulses may change this body.</param>
        /// <returns>Current world inverse inertia, or the zero matrix for a non-responsive participant.</returns>
        static PhysicsMatrix3x3 CreateWorldInverseInertia(
            in HelPhysicsBodyState3D body,
            bool responds) {
            if (!responds) {
                return default;
            }

            PhysicsMatrix3x3 rotation = PhysicsMatrix3x3.CreateFromQuaternion(body.Orientation);
            return rotation * body.LocalInverseInertia * rotation.Transposed();
        }

        /// <summary>
        /// Computes reciprocal positional response along one contact normal from current lever arms and world inertia.
        /// </summary>
        /// <param name="direction">Unit correction direction.</param>
        /// <param name="leverArmA">Current world-space lever arm on body A.</param>
        /// <param name="leverArmB">Current world-space lever arm on body B.</param>
        /// <param name="inverseMassA">Responsive inverse mass of body A.</param>
        /// <param name="inverseMassB">Responsive inverse mass of body B.</param>
        /// <param name="worldInverseInertiaA">Current responsive world inverse inertia of body A.</param>
        /// <param name="worldInverseInertiaB">Current responsive world inverse inertia of body B.</param>
        /// <returns>Reciprocal positive response denominator, or zero when neither participant responds.</returns>
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
        /// Applies one small world-space angular displacement to an orientation and normalizes the result.
        /// </summary>
        /// <param name="orientation">Current normalized body orientation.</param>
        /// <param name="angularCorrection">World-space angular displacement generated by a positional impulse.</param>
        /// <returns>Normalized orientation after the angular correction.</returns>
        static PhysicsQuaternion ApplyAngularCorrection(
            PhysicsQuaternion orientation,
            PhysicsVector3 angularCorrection) {
            PhysicsScalar half = PhysicsScalar.FromFloat(0.5f);
            PhysicsQuaternion corrected = new PhysicsQuaternion(
                orientation.X + (((angularCorrection.X * orientation.W) +
                    (angularCorrection.Y * orientation.Z) -
                    (angularCorrection.Z * orientation.Y)) * half),
                orientation.Y + ((-(angularCorrection.X * orientation.Z) +
                    (angularCorrection.Y * orientation.W) +
                    (angularCorrection.Z * orientation.X)) * half),
                orientation.Z + (((angularCorrection.X * orientation.Y) -
                    (angularCorrection.Y * orientation.X) +
                    (angularCorrection.Z * orientation.W)) * half),
                orientation.W - (((angularCorrection.X * orientation.X) +
                    (angularCorrection.Y * orientation.Y) +
                    (angularCorrection.Z * orientation.Z)) * half));
            return corrected.Normalized();
        }
    }
}
