namespace helengine {
    /// <summary>
    /// Runs the cube-only 3D physics runtime used by the current engine target.
    /// </summary>
    public class PhysicsWorld3D : IPhysicsRuntime {
        /// <summary>
        /// Gravity applied to dynamic bodies each fixed simulation step.
        /// </summary>
        static readonly float3 GravityAcceleration = new float3(0f, -9.81f, 0f);

        /// <summary>
        /// Allowed slop before residual penetration correction starts.
        /// </summary>
        const float PenetrationSlop = 0.001f;

        /// <summary>
        /// Fraction of residual penetration corrected after velocity solving.
        /// </summary>
        const float PositionCorrectionFraction = 1f;

        /// <summary>
        /// Linear speed squared below which a supported body can be treated as quiet.
        /// </summary>
        const double LinearSleepSpeedSquared = 0.0025d;

        /// <summary>
        /// Angular speed squared below which a supported body can be treated as quiet.
        /// </summary>
        const double AngularSleepSpeedSquared = 0.0025d;

        /// <summary>
        /// Angular acceleration used to keep visibly tilted supported cubes rotating in their current fall direction.
        /// </summary>
        const double SupportedTiltAngularAcceleration = 10d;

        /// <summary>
        /// Maximum angular speed introduced by the supported-tilt settling rule.
        /// </summary>
        const double SupportedTiltMaximumAngularSpeed = 4d;

        /// <summary>
        /// Number of quiet supported frames required before a cube is allowed to sleep.
        /// </summary>
        const int QuietFramesBeforeSleep = 24;

        /// <summary>
        /// Cube body states bound to the active scene.
        /// </summary>
        readonly List<CubeBodyState3D> BodyStatesValue;

        /// <summary>
        /// Candidate body pairs reused during each step.
        /// </summary>
        readonly List<CubeBodyPair3D> CandidatePairs;

        /// <summary>
        /// Contact manifolds reused during each substep.
        /// </summary>
        readonly List<CubeRuntimeManifold3D> Manifolds;

        /// <summary>
        /// Per-body quiet support counters used by the cube-only sleep rule.
        /// </summary>
        readonly List<int> QuietFrameCounts;

        /// <summary>
        /// Initializes a cube-only physics world.
        /// </summary>
        /// <param name="settings">Physics settings controlling solver iterations and substeps.</param>
        public PhysicsWorld3D(PhysicsWorld3DSettings settings) {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            BodyStatesValue = new List<CubeBodyState3D>();
            CandidatePairs = new List<CubeBodyPair3D>();
            Manifolds = new List<CubeRuntimeManifold3D>();
            QuietFrameCounts = new List<int>();
        }

        /// <summary>
        /// Gets the effective settings used by the cube runtime.
        /// </summary>
        public PhysicsWorld3DSettings Settings { get; }

        /// <summary>
        /// Gets the cube body states currently bound to this world.
        /// </summary>
        public IReadOnlyList<CubeBodyState3D> BodyStates => BodyStatesValue;

        /// <summary>
        /// Gets the broadphase candidate count from the most recent step.
        /// </summary>
        public int LastBroadphaseCandidatePairCount { get; private set; }

        /// <summary>
        /// Gets scene feature flags for the active cube runtime.
        /// </summary>
        public PhysicsSceneFeatureFlags3D RequiredSceneFeatures { get; private set; }

        /// <summary>
        /// Creates one cube physics world using medium profile settings.
        /// </summary>
        /// <returns>Configured cube-only physics world.</returns>
        public static PhysicsWorld3D CreateMediumDefault() {
            return new PhysicsWorld3D(PhysicsWorld3DSettings.CreateDefault(PhysicsWorld3DProfile.CreateMedium()));
        }

        /// <summary>
        /// Binds supported box rigid bodies from a scene hierarchy.
        /// </summary>
        /// <param name="rootEntities">Root entities to scan.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            BodyStatesValue.Clear();
            CandidatePairs.Clear();
            Manifolds.Clear();
            QuietFrameCounts.Clear();
            RequiredSceneFeatures = PhysicsSceneFeatureFlags3D.BoxBoxContact;
            for (int index = 0; index < rootEntities.Count; index++) {
                CollectCubeBodies(rootEntities[index]);
            }
        }

        /// <summary>
        /// Advances the cube simulation by one fixed step.
        /// </summary>
        /// <param name="stepSeconds">Fixed step duration in seconds.</param>
        public void Step(double stepSeconds) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step size must be a finite value greater than zero.");
            }

            SynchronizeFromScene();
            double substepSeconds = stepSeconds / Settings.SolverSubsteps;
            for (int substepIndex = 0; substepIndex < Settings.SolverSubsteps; substepIndex++) {
                IntegrateVelocities(substepSeconds);
                IntegratePoses(substepSeconds);
                BuildCandidatePairs();
                BuildManifolds();
                SolveManifolds(substepSeconds);
                CorrectResidualPenetration();
                RefreshDerivedState();
                ClampSupportedRestingVelocity();
                ApplySupportedTiltSettling(substepSeconds);
                UpdateActivity();
            }

            SynchronizeToScene();
        }

        /// <summary>
        /// Recursively collects supported cube bodies and rejects unsupported physics components.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        void CollectCubeBodies(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            RigidBody3DComponent rigidBody = FindComponent<RigidBody3DComponent>(entity);
            BoxCollider3DComponent boxCollider = FindComponent<BoxCollider3DComponent>(entity);
            if (FindComponent<SphereCollider3DComponent>(entity) != null ||
                FindComponent<CapsuleCollider3DComponent>(entity) != null ||
                FindComponent<StaticMeshCollider3DComponent>(entity) != null ||
                FindComponent<CharacterController3DComponent>(entity) != null) {
                throw new NotSupportedException("The active 3D physics runtime supports only rigid bodies with box colliders.");
            }

            if (rigidBody != null) {
                if (boxCollider == null) {
                    throw new NotSupportedException("RigidBody3DComponent requires BoxCollider3DComponent in the cube-only physics runtime.");
                }

                BodyStatesValue.Add(new CubeBodyState3D(entity, rigidBody, boxCollider, FindComponent<KinematicMotion3DComponent>(entity)));
                QuietFrameCounts.Add(0);
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                CollectCubeBodies(entity.Children[index]);
            }
        }

        /// <summary>
        /// Finds one component of the requested type on the supplied entity.
        /// </summary>
        /// <typeparam name="TComponent">Requested component type.</typeparam>
        /// <param name="entity">Entity being queried.</param>
        /// <returns>Attached component when present; otherwise null.</returns>
        static TComponent FindComponent<TComponent>(Entity entity) where TComponent : Component {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is TComponent component) {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// Synchronizes body state from authored entities.
        /// </summary>
        void SynchronizeFromScene() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].SynchronizeFromEntity();
            }
        }

        /// <summary>
        /// Writes body state back to authored entities.
        /// </summary>
        void SynchronizeToScene() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].SynchronizeToEntity();
            }
        }

        /// <summary>
        /// Integrates gravity and kinematic velocities.
        /// </summary>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        void IntegrateVelocities(double stepSeconds) {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                CubeBodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind == BodyKind3D.Kinematic) {
                    AdvanceKinematicVelocity(bodyState, stepSeconds);
                    continue;
                }
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic || !bodyState.RigidBody.UseGravity) {
                    continue;
                }

                bodyState.Velocity = bodyState.Velocity + (GravityAcceleration * (float)(bodyState.RigidBody.GravityScale * stepSeconds));
            }
        }

        /// <summary>
        /// Advances one kinematic body's authored velocity or motion path.
        /// </summary>
        /// <param name="bodyState">Body state to update.</param>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        static void AdvanceKinematicVelocity(CubeBodyState3D bodyState, double stepSeconds) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            if (bodyState.KinematicMotionComponent == null) {
                bodyState.Velocity = bodyState.RigidBody.LinearVelocity;
                return;
            }

            double elapsedSeconds = bodyState.KinematicMotionElapsedSeconds + stepSeconds;
            bodyState.KinematicMotionElapsedSeconds = elapsedSeconds;
            float3 targetPosition = ResolveKinematicTargetPosition(bodyState.KinematicMotionComponent, elapsedSeconds);
            bodyState.Velocity = (targetPosition - bodyState.Position) / (float)stepSeconds;
        }

        /// <summary>
        /// Resolves the target local position for a kinematic box motion path.
        /// </summary>
        /// <param name="motion">Authored kinematic motion path.</param>
        /// <param name="elapsedSeconds">Elapsed runtime seconds.</param>
        /// <returns>Interpolated local position on the motion path.</returns>
        static float3 ResolveKinematicTargetPosition(KinematicMotion3DComponent motion, double elapsedSeconds) {
            if (motion == null) {
                throw new ArgumentNullException(nameof(motion));
            }

            double normalized = elapsedSeconds / motion.TravelDurationSeconds;
            if (motion.PingPong) {
                double cycle = normalized % 2d;
                if (cycle < 0d) {
                    cycle += 2d;
                }
                if (cycle > 1d) {
                    normalized = 2d - cycle;
                } else {
                    normalized = cycle;
                }
            } else {
                normalized = Math.Min(1d, Math.Max(0d, normalized));
            }

            float amount = (float)normalized;
            return motion.StartLocalPosition + ((motion.EndLocalPosition - motion.StartLocalPosition) * amount);
        }

        /// <summary>
        /// Integrates body positions and orientations from current velocities.
        /// </summary>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        void IntegratePoses(double stepSeconds) {
            float stepSecondsFloat = (float)stepSeconds;
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                CubeBodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind == BodyKind3D.Static) {
                    continue;
                }

                bodyState.Position = bodyState.Position + (bodyState.Velocity * stepSecondsFloat);
                IntegrateOrientation(bodyState, stepSecondsFloat);
                bodyState.RefreshDerivedShapeState();
            }
        }

        /// <summary>
        /// Integrates one body's orientation from angular velocity.
        /// </summary>
        /// <param name="bodyState">Body state to rotate.</param>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        static void IntegrateOrientation(CubeBodyState3D bodyState, float stepSeconds) {
            float3 angularVelocity = bodyState.AngularVelocity;
            double angularSpeedSquared = float3.Dot(angularVelocity, angularVelocity);
            if (angularSpeedSquared <= 0.0000001d) {
                return;
            }

            double angularSpeed = Math.Sqrt(angularSpeedSquared);
            float3 axis = angularVelocity / (float)angularSpeed;
            float4.CreateFromAxisAngle(ref axis, (float)(angularSpeed * stepSeconds), out float4 deltaRotation);
            float4 orientation = bodyState.Orientation * deltaRotation;
            orientation.Normalize();
            bodyState.Orientation = orientation;
        }

        /// <summary>
        /// Builds all cube pair candidates using a simple deterministic all-pairs scan.
        /// </summary>
        void BuildCandidatePairs() {
            CandidatePairs.Clear();
            for (int firstIndex = 0; firstIndex < BodyStatesValue.Count; firstIndex++) {
                for (int secondIndex = firstIndex + 1; secondIndex < BodyStatesValue.Count; secondIndex++) {
                    CubeBodyState3D first = BodyStatesValue[firstIndex];
                    CubeBodyState3D second = BodyStatesValue[secondIndex];
                    if (first.RigidBody.BodyKind == BodyKind3D.Static && second.RigidBody.BodyKind == BodyKind3D.Static) {
                        continue;
                    }
                    if (!CanCollide(first.Collider, second.Collider)) {
                        continue;
                    }

                    CandidatePairs.Add(new CubeBodyPair3D(firstIndex, secondIndex));
                }
            }

            LastBroadphaseCandidatePairCount = CandidatePairs.Count;
        }

        /// <summary>
        /// Builds contact manifolds for currently overlapping cube pairs.
        /// </summary>
        void BuildManifolds() {
            Manifolds.Clear();
            for (int pairIndex = 0; pairIndex < CandidatePairs.Count; pairIndex++) {
                CubeBodyPair3D pair = CandidatePairs[pairIndex];
                CubeBodyState3D first = BodyStatesValue[pair.FirstBodyIndex];
                CubeBodyState3D second = BodyStatesValue[pair.SecondBodyIndex];
                if (first.Collider.IsTrigger || second.Collider.IsTrigger) {
                    continue;
                }
                if (!CubeBoxContactResolver3D.TryResolveManifold(first, second, out CubeContactManifold3D manifold)) {
                    continue;
                }

                Manifolds.Add(new CubeRuntimeManifold3D(pair.FirstBodyIndex, pair.SecondBodyIndex, manifold));
            }
        }

        /// <summary>
        /// Solves normal and friction impulses for every manifold.
        /// </summary>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        void SolveManifolds(double stepSeconds) {
            for (int iteration = 0; iteration < Settings.SolverIterations; iteration++) {
                for (int manifoldIndex = 0; manifoldIndex < Manifolds.Count; manifoldIndex++) {
                    CubeRuntimeManifold3D manifold = Manifolds[manifoldIndex];
                    CubeBodyState3D first = BodyStatesValue[manifold.FirstBodyIndex];
                    CubeBodyState3D second = BodyStatesValue[manifold.SecondBodyIndex];
                    for (int pointIndex = 0; pointIndex < manifold.Contact.ContactCount; pointIndex++) {
                        SolveContactPoint(first, second, manifold.Contact, manifold.Contact.GetPoint(pointIndex), stepSeconds);
                    }
                }
            }
        }

        /// <summary>
        /// Solves one contact point using sequential impulse response.
        /// </summary>
        /// <param name="first">First body in the contact.</param>
        /// <param name="second">Second body in the contact.</param>
        /// <param name="manifold">Contact manifold containing the normal.</param>
        /// <param name="point">Contact point to solve.</param>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        static void SolveContactPoint(CubeBodyState3D first, CubeBodyState3D second, CubeContactManifold3D manifold, CubeContactPoint3D point, double stepSeconds) {
            float3 normal = manifold.Normal;
            float3 firstOffset = point.Position - first.Position;
            float3 secondOffset = point.Position - second.Position;
            float3 firstVelocity = first.Velocity + float3.Cross(first.AngularVelocity, firstOffset);
            float3 secondVelocity = second.Velocity + float3.Cross(second.AngularVelocity, secondOffset);
            float3 relativeVelocity = firstVelocity - secondVelocity;
            double closingSpeed = float3.Dot(relativeVelocity, normal);
            double numerator = -closingSpeed;
            if (numerator <= 0d) {
                return;
            }

            double effectiveMass = ResolveEffectiveMass(first, second, normal, firstOffset, secondOffset);
            if (effectiveMass <= 0d) {
                return;
            }

            float3 impulse = normal * (float)(numerator / effectiveMass);
            ApplyImpulse(first, impulse, firstOffset);
            ApplyImpulse(second, impulse * -1f, secondOffset);
            SolveFriction(first, second, normal, point.Position, impulse);
        }

        /// <summary>
        /// Applies a friction impulse for one solved normal contact.
        /// </summary>
        /// <param name="first">First body in the contact.</param>
        /// <param name="second">Second body in the contact.</param>
        /// <param name="normal">Contact normal.</param>
        /// <param name="contactPoint">World-space contact point.</param>
        /// <param name="normalImpulse">Normal impulse already applied.</param>
        static void SolveFriction(CubeBodyState3D first, CubeBodyState3D second, float3 normal, float3 contactPoint, float3 normalImpulse) {
            float3 firstOffset = contactPoint - first.Position;
            float3 secondOffset = contactPoint - second.Position;
            float3 firstVelocity = first.Velocity + float3.Cross(first.AngularVelocity, firstOffset);
            float3 secondVelocity = second.Velocity + float3.Cross(second.AngularVelocity, secondOffset);
            float3 relativeVelocity = firstVelocity - secondVelocity;
            float3 tangent = relativeVelocity - (normal * float3.Dot(relativeVelocity, normal));
            double tangentSpeedSquared = float3.Dot(tangent, tangent);
            if (tangentSpeedSquared <= 0.000001d) {
                return;
            }

            tangent /= (float)Math.Sqrt(tangentSpeedSquared);
            double effectiveMass = ResolveEffectiveMass(first, second, tangent, firstOffset, secondOffset);
            if (effectiveMass <= 0d) {
                return;
            }

            double tangentSpeed = float3.Dot(relativeVelocity, tangent);
            double desiredMagnitude = -tangentSpeed / effectiveMass;
            double friction = (first.Collider.DynamicFriction + second.Collider.DynamicFriction) * 0.5d;
            double maximumMagnitude = Math.Sqrt(float3.Dot(normalImpulse, normalImpulse)) * friction;
            double clampedMagnitude = Math.Max(-maximumMagnitude, Math.Min(maximumMagnitude, desiredMagnitude));
            float3 frictionImpulse = tangent * (float)clampedMagnitude;
            ApplyImpulse(first, frictionImpulse, firstOffset);
            ApplyImpulse(second, frictionImpulse * -1f, secondOffset);
        }

        /// <summary>
        /// Resolves effective mass for one impulse direction at a contact point.
        /// </summary>
        /// <param name="first">First body.</param>
        /// <param name="second">Second body.</param>
        /// <param name="direction">Impulse direction.</param>
        /// <param name="firstOffset">First contact offset from center of mass.</param>
        /// <param name="secondOffset">Second contact offset from center of mass.</param>
        /// <returns>Effective inverse mass.</returns>
        static double ResolveEffectiveMass(CubeBodyState3D first, CubeBodyState3D second, float3 direction, float3 firstOffset, float3 secondOffset) {
            double inverseMass = ResolveInverseMass(first) + ResolveInverseMass(second);
            inverseMass += ResolveAngularEffectiveMass(first, direction, firstOffset);
            inverseMass += ResolveAngularEffectiveMass(second, direction, secondOffset);
            return inverseMass;
        }

        /// <summary>
        /// Resolves angular contribution to effective mass.
        /// </summary>
        /// <param name="bodyState">Body whose inertia should contribute.</param>
        /// <param name="direction">Impulse direction.</param>
        /// <param name="offset">Contact offset from center of mass.</param>
        /// <returns>Angular effective inverse mass.</returns>
        static double ResolveAngularEffectiveMass(CubeBodyState3D bodyState, float3 direction, float3 offset) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return 0d;
            }

            float3 angular = float3.Cross(offset, direction);
            float3 inertiaAngular = new float3(
                angular.X * bodyState.InverseInertia.X,
                angular.Y * bodyState.InverseInertia.Y,
                angular.Z * bodyState.InverseInertia.Z);
            return float3.Dot(float3.Cross(inertiaAngular, offset), direction);
        }

        /// <summary>
        /// Applies an impulse to one dynamic body.
        /// </summary>
        /// <param name="bodyState">Body receiving the impulse.</param>
        /// <param name="impulse">World-space impulse.</param>
        /// <param name="offset">Contact offset from center of mass.</param>
        static void ApplyImpulse(CubeBodyState3D bodyState, float3 impulse, float3 offset) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            float inverseMass = (float)ResolveInverseMass(bodyState);
            bodyState.Velocity = bodyState.Velocity + (impulse * inverseMass);
            float3 angularImpulse = float3.Cross(offset, impulse);
            bodyState.AngularVelocity = bodyState.AngularVelocity + new float3(
                angularImpulse.X * bodyState.InverseInertia.X,
                angularImpulse.Y * bodyState.InverseInertia.Y,
                angularImpulse.Z * bodyState.InverseInertia.Z);
        }

        /// <summary>
        /// Applies small residual positional correction after impulse solving.
        /// </summary>
        void CorrectResidualPenetration() {
            for (int manifoldIndex = 0; manifoldIndex < Manifolds.Count; manifoldIndex++) {
                CubeRuntimeManifold3D manifold = Manifolds[manifoldIndex];
                CubeBodyState3D first = BodyStatesValue[manifold.FirstBodyIndex];
                CubeBodyState3D second = BodyStatesValue[manifold.SecondBodyIndex];
                float maximumPenetration = ResolveMaximumPenetration(manifold.Contact);
                float correction = Math.Max(0f, maximumPenetration - PenetrationSlop) * PositionCorrectionFraction;
                if (correction <= 0f) {
                    continue;
                }

                double firstInverseMass = ResolveInverseMass(first);
                double secondInverseMass = ResolveInverseMass(second);
                double inverseMassSum = firstInverseMass + secondInverseMass;
                if (inverseMassSum <= 0d) {
                    continue;
                }

                if (firstInverseMass > 0d) {
                    first.Position = first.Position + (manifold.Contact.Normal * (float)(correction * firstInverseMass / inverseMassSum));
                }
                if (secondInverseMass > 0d) {
                    second.Position = second.Position - (manifold.Contact.Normal * (float)(correction * secondInverseMass / inverseMassSum));
                }
            }
        }

        /// <summary>
        /// Updates derived box bounds after positional correction.
        /// </summary>
        void RefreshDerivedState() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].RefreshDerivedShapeState();
            }
        }

        /// <summary>
        /// Removes residual velocity into support contacts after the iterative solver has settled a cube.
        /// </summary>
        void ClampSupportedRestingVelocity() {
            for (int manifoldIndex = 0; manifoldIndex < Manifolds.Count; manifoldIndex++) {
                CubeRuntimeManifold3D manifold = Manifolds[manifoldIndex];
                ClampBodyVelocityOutOfContact(BodyStatesValue[manifold.FirstBodyIndex], manifold.Contact.Normal);
                ClampBodyVelocityOutOfContact(BodyStatesValue[manifold.SecondBodyIndex], manifold.Contact.Normal * -1f);
            }
        }

        /// <summary>
        /// Removes linear velocity that points into a contact plane for one dynamic body.
        /// </summary>
        /// <param name="bodyState">Body whose velocity should be clamped.</param>
        /// <param name="outwardNormal">Normal pointing away from the opposing body for this body.</param>
        static void ClampBodyVelocityOutOfContact(CubeBodyState3D bodyState, float3 outwardNormal) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            double normalSpeed = float3.Dot(bodyState.Velocity, outwardNormal);
            if (outwardNormal.Y > 0.6f && normalSpeed < 1d && IsVisuallyFlat(bodyState)) {
                bodyState.Velocity = bodyState.Velocity - (outwardNormal * (float)normalSpeed);
                return;
            }
            if (normalSpeed >= 0.1d) {
                return;
            }

            bodyState.Velocity = bodyState.Velocity - (outwardNormal * (float)normalSpeed);
        }

        /// <summary>
        /// Updates simple repeated-low-motion sleep state.
        /// </summary>
        void UpdateActivity() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                CubeBodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                bool supported = IsSupported(index);
                double linearSpeedSquared = float3.Dot(bodyState.Velocity, bodyState.Velocity);
                double angularSpeedSquared = float3.Dot(bodyState.AngularVelocity, bodyState.AngularVelocity);
                if (supported && linearSpeedSquared <= LinearSleepSpeedSquared && angularSpeedSquared <= AngularSleepSpeedSquared) {
                    QuietFrameCounts[index]++;
                } else {
                    QuietFrameCounts[index] = 0;
                }

                if (QuietFrameCounts[index] >= QuietFramesBeforeSleep && IsVisuallyFlat(bodyState)) {
                    bodyState.Velocity = float3.Zero;
                    bodyState.AngularVelocity = float3.Zero;
                }
            }
        }

        /// <summary>
        /// Determines whether a quiet supported cube is already flat enough to sleep without any visual pose correction.
        /// </summary>
        /// <param name="bodyState">Quiet supported body being considered for sleep.</param>
        /// <returns>True when the body can sleep without a visible snap; otherwise false.</returns>
        static bool IsVisuallyFlat(CubeBodyState3D bodyState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            float3 localUp = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            return Math.Abs(localUp.Y) >= 0.995f;
        }

        /// <summary>
        /// Adds smooth angular motion for tilted supported cubes that would otherwise stall on an edge.
        /// </summary>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        void ApplySupportedTiltSettling(double stepSeconds) {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                CubeBodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }
                if (!IsSupported(index) || IsVisuallyFlat(bodyState)) {
                    continue;
                }

                ApplyTiltSettlingAngularVelocity(bodyState, stepSeconds);
            }
        }

        /// <summary>
        /// Applies angular velocity that continues the tilted cube's current rotation direction.
        /// </summary>
        /// <param name="bodyState">Tilted supported body to rotate.</param>
        /// <param name="stepSeconds">Substep duration in seconds.</param>
        static void ApplyTiltSettlingAngularVelocity(CubeBodyState3D bodyState, double stepSeconds) {
            double speedSquared = float3.Dot(bodyState.AngularVelocity, bodyState.AngularVelocity);
            if (speedSquared <= 0.000001d) {
                return;
            }

            double speed = Math.Sqrt(speedSquared);
            float3 rotationAxis = bodyState.AngularVelocity * (float)(1d / speed);
            float3 angularVelocityDelta = rotationAxis * (float)(SupportedTiltAngularAcceleration * stepSeconds);
            bodyState.AngularVelocity = ClampAngularVelocity(bodyState.AngularVelocity + angularVelocityDelta, SupportedTiltMaximumAngularSpeed);
        }

        /// <summary>
        /// Clamps angular velocity to a maximum magnitude.
        /// </summary>
        /// <param name="angularVelocity">Angular velocity to clamp.</param>
        /// <param name="maximumAngularSpeed">Maximum allowed angular speed.</param>
        /// <returns>Original angular velocity when inside the limit; otherwise a scaled velocity.</returns>
        static float3 ClampAngularVelocity(float3 angularVelocity, double maximumAngularSpeed) {
            double speedSquared = float3.Dot(angularVelocity, angularVelocity);
            double maximumSpeedSquared = maximumAngularSpeed * maximumAngularSpeed;
            if (speedSquared <= maximumSpeedSquared) {
                return angularVelocity;
            }

            double speed = Math.Sqrt(speedSquared);
            return angularVelocity * (float)(maximumAngularSpeed / speed);
        }

        /// <summary>
        /// Determines whether a body has an upward contact below its center.
        /// </summary>
        /// <param name="bodyIndex">Body index to inspect.</param>
        /// <returns>True when the body has a support contact.</returns>
        bool IsSupported(int bodyIndex) {
            CubeBodyState3D bodyState = BodyStatesValue[bodyIndex];
            for (int manifoldIndex = 0; manifoldIndex < Manifolds.Count; manifoldIndex++) {
                CubeRuntimeManifold3D manifold = Manifolds[manifoldIndex];
                if (manifold.FirstBodyIndex == bodyIndex && manifold.Contact.Normal.Y > 0.6f && manifold.Contact.Point0.Position.Y <= bodyState.Position.Y) {
                    return true;
                }
                if (manifold.SecondBodyIndex == bodyIndex && manifold.Contact.Normal.Y < -0.6f && manifold.Contact.Point0.Position.Y <= bodyState.Position.Y) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the largest penetration in a manifold.
        /// </summary>
        /// <param name="manifold">Contact manifold to inspect.</param>
        /// <returns>Largest penetration value.</returns>
        static float ResolveMaximumPenetration(CubeContactManifold3D manifold) {
            float penetration = 0f;
            for (int index = 0; index < manifold.ContactCount; index++) {
                penetration = Math.Max(penetration, manifold.GetPoint(index).Penetration);
            }

            return penetration;
        }

        /// <summary>
        /// Resolves inverse mass for a body.
        /// </summary>
        /// <param name="bodyState">Body state to inspect.</param>
        /// <returns>Inverse mass for dynamic bodies; otherwise zero.</returns>
        static double ResolveInverseMass(CubeBodyState3D bodyState) {
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return 0d;
            }

            return 1d / bodyState.RigidBody.Mass;
        }

        /// <summary>
        /// Determines whether two colliders pass layer and mask filtering.
        /// </summary>
        /// <param name="first">First collider.</param>
        /// <param name="second">Second collider.</param>
        /// <returns>True when the colliders can interact.</returns>
        static bool CanCollide(BoxCollider3DComponent first, BoxCollider3DComponent second) {
            return (first.CollisionMask & second.CollisionLayer) != 0 &&
                (second.CollisionMask & first.CollisionLayer) != 0;
        }
    }
}
