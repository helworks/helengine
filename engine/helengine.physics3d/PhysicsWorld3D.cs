namespace helengine {
    /// <summary>
    /// Represents one hosted 3D physics world configured for a specific runtime profile.
    /// </summary>
    public class PhysicsWorld3D : IPhysicsRuntime {
        /// <summary>
        /// Gravity applied to dynamic bodies each fixed step.
        /// </summary>
        static readonly float3 GravityAcceleration = new float3(0f, -9.81f, 0f);

        /// <summary>
        /// Angular damping rate applied only to upright contacted boxes to drain settled contact jitter.
        /// </summary>
        const double RestingUprightAngularDampingPerSecond = 16d;

        /// <summary>
        /// Angular speed squared below which solver noise is treated as fully settled.
        /// </summary>
        const double AngularSleepSpeedSquared = 0.0001d;

        /// <summary>
        /// Maximum angular speed allowed for one dynamic body after contact solving.
        /// </summary>
        const double MaximumDynamicAngularSpeed = 10d;

        /// <summary>
        /// Linear speed squared below which a contacted dynamic body is treated as resting.
        /// </summary>
        const double ContactLinearSleepSpeedSquared = 0.04d;

        /// <summary>
        /// Angular speed squared below which a contacted dynamic body is treated as resting.
        /// </summary>
        const double ContactAngularSleepSpeedSquared = 0.04d;

        /// <summary>
        /// Angular damping applied to tilted bodies resting on stable support footprints.
        /// </summary>
        const float StableTiltedContactAngularDamping = 0.2f;

        /// <summary>
        /// Minimum current upright alignment required before a resting box can be stabilized without flipping side-resting bodies.
        /// </summary>
        const float RestingUprightCandidateYThreshold = 0.85f;

        /// <summary>
        /// Minimum speculative contact distance used when relative velocity is very small.
        /// </summary>
        const float MinimumSpeculativeContactMargin = 0.05f;

        /// <summary>
        /// Dense body-state list currently bound to the runtime scene.
        /// </summary>
        readonly List<BodyState3D> BodyStatesValue;

        /// <summary>
        /// Dense character-controller state list currently bound to the runtime scene.
        /// </summary>
        readonly List<CharacterControllerState3D> ControllerStatesValue;

        /// <summary>
        /// Dense cooked static-mesh state list currently bound to the runtime scene.
        /// </summary>
        readonly List<StaticMeshBodyState3D> StaticMeshStatesValue;

        /// <summary>
        /// Broadphase implementation used to reduce contact-solver candidate pairs.
        /// </summary>
        readonly IBroadphase3D BroadphaseValue;

        /// <summary>
        /// Trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        readonly List<TriggerEvent3D> TriggerEventsValue;

        /// <summary>
        /// Trigger overlap pairs that remained active after the previous fixed step completed.
        /// </summary>
        readonly List<TriggerPairKey3D> ActiveTriggerPairsValue;

        /// <summary>
        /// Trigger overlap pairs detected during the current fixed step.
        /// </summary>
        readonly List<TriggerPairKey3D> CurrentTriggerPairsValue;

        /// <summary>
        /// Warm-started box-box contact constraints keyed by persistent body pair.
        /// </summary>
        readonly List<BoxBoxContactConstraint3D> BoxBoxContactConstraintsValue;

        /// <summary>
        /// Initializes a new 3D physics world.
        /// </summary>
        /// <param name="settings">Effective world settings resolved from profile defaults and local overrides.</param>
        public PhysicsWorld3D(PhysicsWorld3DSettings settings) {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            BodyStatesValue = new List<BodyState3D>();
            ControllerStatesValue = new List<CharacterControllerState3D>();
            StaticMeshStatesValue = new List<StaticMeshBodyState3D>();
            TriggerEventsValue = new List<TriggerEvent3D>();
            ActiveTriggerPairsValue = new List<TriggerPairKey3D>();
            CurrentTriggerPairsValue = new List<TriggerPairKey3D>();
            BoxBoxContactConstraintsValue = new List<BoxBoxContactConstraint3D>();
            BroadphaseValue = CreateBroadphase(settings);
        }

        /// <summary>
        /// Gets the effective settings that constrain this world.
        /// </summary>
        public PhysicsWorld3DSettings Settings { get; }

        /// <summary>
        /// Gets the currently bound runtime body states.
        /// </summary>
        public IReadOnlyList<BodyState3D> BodyStates => BodyStatesValue;

        /// <summary>
        /// Gets the currently bound runtime character-controller states.
        /// </summary>
        public IReadOnlyList<CharacterControllerState3D> ControllerStates => ControllerStatesValue;

        /// <summary>
        /// Gets the candidate-pair count generated by the most recent broadphase update.
        /// </summary>
        public int LastBroadphaseCandidatePairCount { get; private set; }

        /// <summary>
        /// Gets the trigger overlap events emitted during the most recent fixed step.
        /// </summary>
        public IReadOnlyList<TriggerEvent3D> TriggerEvents => TriggerEventsValue;

        /// <summary>
        /// Gets the scene feature flags inferred from the currently bound authored scene hierarchy.
        /// </summary>
        public PhysicsSceneFeatureFlags3D RequiredSceneFeatures { get; private set; }

        /// <summary>
        /// Binds one scene hierarchy to the world by discovering supported rigid-body entities.
        /// </summary>
        /// <param name="rootEntities">Root entities that own the active scene hierarchy.</param>
        public void BindScene(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            BodyStatesValue.Clear();
            ControllerStatesValue.Clear();
            StaticMeshStatesValue.Clear();
            TriggerEventsValue.Clear();
            ActiveTriggerPairsValue.Clear();
            CurrentTriggerPairsValue.Clear();
            BoxBoxContactConstraintsValue.Clear();
            RequiredSceneFeatures = PhysicsSceneFeatureAnalyzer3D.Analyze(rootEntities);
            for (int index = 0; index < rootEntities.Count; index++) {
                CollectBodyStates(rootEntities[index]);
            }
        }

        /// <summary>
        /// Advances the world by one fixed simulation step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        public void Step(double stepSeconds) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step size must be a finite value greater than zero.");
            }

            SynchronizeFromScene();
            ClearStepContactState();
            AdvanceKinematicBodies(stepSeconds);
            AdvanceCharacterControllers(stepSeconds);
            IntegrateDynamicBodyVelocities(stepSeconds);
            ResolveSpeculativeBoxBoxContacts(stepSeconds);
            IntegrateDynamicBodyPoses(stepSeconds);
            ResolveContacts(stepSeconds, true);
            ApplyDynamicDamping(stepSeconds);
            SynchronizeToScene();
        }

        /// <summary>
        /// Creates one world using the medium runtime profile defaults.
        /// </summary>
        /// <returns>Configured 3D physics world.</returns>
        public static PhysicsWorld3D CreateMediumDefault() {
            PhysicsWorld3DProfile profile = PhysicsWorld3DProfile.CreateMedium();
            PhysicsWorld3DSettings settings = PhysicsWorld3DSettings.CreateDefault(profile);
            return new PhysicsWorld3D(settings);
        }

        /// <summary>
        /// Rebuilds runtime body states from the current authored entity transforms.
        /// </summary>
        void SynchronizeFromScene() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].SynchronizeFromEntity();
            }

            for (int index = 0; index < ControllerStatesValue.Count; index++) {
                ControllerStatesValue[index].SynchronizeFromEntity();
            }

            for (int index = 0; index < StaticMeshStatesValue.Count; index++) {
                StaticMeshStatesValue[index].SynchronizeFromEntity();
            }
        }

        /// <summary>
        /// Advances every dynamic body's velocity by one gravity-integrated fixed step.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void IntegrateDynamicBodyVelocities(double stepSeconds) {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                float3 velocity = bodyState.Velocity;
                if (bodyState.RigidBody.UseGravity) {
                    velocity = new float3(
                        velocity.X + (float)(GravityAcceleration.X * bodyState.RigidBody.GravityScale * stepSeconds),
                        velocity.Y + (float)(GravityAcceleration.Y * bodyState.RigidBody.GravityScale * stepSeconds),
                        velocity.Z + (float)(GravityAcceleration.Z * bodyState.RigidBody.GravityScale * stepSeconds));
                }

                bodyState.Velocity = velocity;
            }
        }

        /// <summary>
        /// Advances every dynamic body's pose using the velocities produced by integration and contact solving.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void IntegrateDynamicBodyPoses(double stepSeconds) {
            float stepSecondsFloat = (float)stepSeconds;
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                bodyState.Position = new float3(
                    bodyState.Position.X + (bodyState.Velocity.X * stepSecondsFloat),
                    bodyState.Position.Y + (bodyState.Velocity.Y * stepSecondsFloat),
                    bodyState.Position.Z + (bodyState.Velocity.Z * stepSecondsFloat));
                IntegrateDynamicBodyOrientation(bodyState, stepSecondsFloat);
                bodyState.RefreshDerivedShapeState();
            }
        }

        /// <summary>
        /// Clears transient contact participation flags before the current step resolves new contacts.
        /// </summary>
        void ClearStepContactState() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].ContactWasResolvedThisStep = false;
                BodyStatesValue[index].MaximumContactNormalY = 0f;
                BodyStatesValue[index].HasUnstableSupportContactThisStep = false;
                BodyStatesValue[index].HasStableSupportContactThisStep = false;
                BodyStatesValue[index].MaximumNormalContactLeverArmXZ = 0f;
            }
        }

        /// <summary>
        /// Advances one dynamic body's orientation from its angular velocity.
        /// </summary>
        /// <param name="bodyState">Dynamic body state to rotate.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        static void IntegrateDynamicBodyOrientation(BodyState3D bodyState, float stepSeconds) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            float3 angularVelocity = bodyState.AngularVelocity;
            double angularSpeedSquared =
                (angularVelocity.X * angularVelocity.X) +
                (angularVelocity.Y * angularVelocity.Y) +
                (angularVelocity.Z * angularVelocity.Z);
            if (angularSpeedSquared <= 0.0000001d) {
                return;
            }

            double angularSpeed = Math.Sqrt(angularSpeedSquared);
            float3 axis = angularVelocity / (float)angularSpeed;
            float4.CreateFromAxisAngle(ref axis, (float)(angularSpeed * stepSeconds), out float4 deltaRotation);
            float4 orientation = deltaRotation * bodyState.Orientation;
            orientation.Normalize();
            bodyState.Orientation = orientation;
        }

        /// <summary>
        /// Applies world-default damping to dynamic body velocities after contact resolution.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void ApplyDynamicDamping(double stepSeconds) {
            double angularDamping = Math.Max(0d, 1d - (RestingUprightAngularDampingPerSecond * stepSeconds));
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                bool isRestingUprightCandidate = IsRestingUprightCandidate(bodyState);
                bool canApplyRestingAngularDamping = isRestingUprightCandidate &&
                    bodyState.ContactWasResolvedThisStep &&
                    bodyState.HasStableSupportContactThisStep &&
                    !bodyState.HasUnstableSupportContactThisStep;
                float3 angularVelocity = canApplyRestingAngularDamping
                    ? bodyState.AngularVelocity * (float)angularDamping
                    : bodyState.AngularVelocity;
                double angularSpeedSquared =
                    (angularVelocity.X * angularVelocity.X) +
                    (angularVelocity.Y * angularVelocity.Y) +
                    (angularVelocity.Z * angularVelocity.Z);
                double linearSpeedSquared =
                    (bodyState.Velocity.X * bodyState.Velocity.X) +
                    (bodyState.Velocity.Y * bodyState.Velocity.Y) +
                    (bodyState.Velocity.Z * bodyState.Velocity.Z);
                if (canApplyRestingAngularDamping && linearSpeedSquared <= ContactLinearSleepSpeedSquared && angularSpeedSquared <= AngularSleepSpeedSquared) {
                    bodyState.AngularVelocity = float3.Zero;
                } else {
                    bodyState.AngularVelocity = angularVelocity;
                }

                if (bodyState.ContactWasResolvedThisStep) {
                    ApplyContactSleep(bodyState);
                }
                ClampDynamicAngularVelocity(bodyState);
            }
        }

        /// <summary>
        /// Limits angular velocity so a bad contact island cannot carry nonphysical spin into the next frame.
        /// </summary>
        /// <param name="bodyState">Dynamic body whose angular velocity should be constrained.</param>
        static void ClampDynamicAngularVelocity(BodyState3D bodyState) {
            double angularSpeedSquared =
                (bodyState.AngularVelocity.X * bodyState.AngularVelocity.X) +
                (bodyState.AngularVelocity.Y * bodyState.AngularVelocity.Y) +
                (bodyState.AngularVelocity.Z * bodyState.AngularVelocity.Z);
            double maximumAngularSpeedSquared = MaximumDynamicAngularSpeed * MaximumDynamicAngularSpeed;
            if (angularSpeedSquared <= maximumAngularSpeedSquared) {
                return;
            }

            double angularSpeed = Math.Sqrt(angularSpeedSquared);
            bodyState.AngularVelocity = bodyState.AngularVelocity * (float)(MaximumDynamicAngularSpeed / angularSpeed);
        }

        /// <summary>
        /// Snaps tiny residual velocities to zero for a body that already resolved solid contact this step.
        /// </summary>
        /// <param name="bodyState">Dynamic body that may be resting on contact.</param>
        static void ApplyContactSleep(BodyState3D bodyState) {
            double linearSpeedSquared =
                (bodyState.Velocity.X * bodyState.Velocity.X) +
                (bodyState.Velocity.Y * bodyState.Velocity.Y) +
                (bodyState.Velocity.Z * bodyState.Velocity.Z);
            bool linearCanSleep = linearSpeedSquared <= ContactLinearSleepSpeedSquared;

            bool isRestingUprightCandidate = IsRestingUprightCandidate(bodyState);
            if (linearCanSleep && bodyState.HasStableSupportContactThisStep && !isRestingUprightCandidate) {
                bodyState.AngularVelocity = bodyState.AngularVelocity * StableTiltedContactAngularDamping;
            }

            double angularSpeedSquared =
                (bodyState.AngularVelocity.X * bodyState.AngularVelocity.X) +
                (bodyState.AngularVelocity.Y * bodyState.AngularVelocity.Y) +
                (bodyState.AngularVelocity.Z * bodyState.AngularVelocity.Z);
            bool angularCanSleep = angularSpeedSquared <= ContactAngularSleepSpeedSquared;
            bool contactCanAngularSleep = bodyState.HasStableSupportContactThisStep || !bodyState.HasUnstableSupportContactThisStep;
            if (linearCanSleep) {
                bodyState.Velocity = float3.Zero;
            }
            if (angularCanSleep && contactCanAngularSleep && isRestingUprightCandidate) {
                bodyState.AngularVelocity = float3.Zero;
            }
        }

        /// <summary>
        /// Advances authored kinematic bodies from their motion components or authored linear velocities before dynamic integration runs.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void AdvanceKinematicBodies(double stepSeconds) {
            float stepSecondsFloat = (float)stepSeconds;
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyState3D bodyState = BodyStatesValue[index];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Kinematic) {
                    continue;
                }

                float3 previousPosition = bodyState.Position;
                if (bodyState.KinematicMotionComponent != null) {
                    bodyState.KinematicMotionElapsedSeconds = bodyState.KinematicMotionElapsedSeconds + stepSeconds;
                    bodyState.Position = EvaluateKinematicMotionPosition(bodyState.KinematicMotionComponent, bodyState.KinematicMotionElapsedSeconds);
                } else {
                    bodyState.Position = new float3(
                        bodyState.Position.X + (bodyState.Velocity.X * stepSecondsFloat),
                        bodyState.Position.Y + (bodyState.Velocity.Y * stepSecondsFloat),
                        bodyState.Position.Z + (bodyState.Velocity.Z * stepSecondsFloat));
                }

                bodyState.Velocity = new float3(
                    (bodyState.Position.X - previousPosition.X) / stepSecondsFloat,
                    (bodyState.Position.Y - previousPosition.Y) / stepSecondsFloat,
                    (bodyState.Position.Z - previousPosition.Z) / stepSecondsFloat);
            }
        }

        /// <summary>
        /// Advances every character controller by applying authored planar motion, gravity, and support-height snapping.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        void AdvanceCharacterControllers(double stepSeconds) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER
            for (int index = 0; index < ControllerStatesValue.Count; index++) {
                CharacterControllerStepResolver3D.Advance(ControllerStatesValue[index], BodyStatesValue, StaticMeshStatesValue, GravityAcceleration, stepSeconds);
            }
#endif
        }

        /// <summary>
        /// Resolves overlapping box contacts using a simple iterative axis-aligned solver.
        /// </summary>
        void ResolveSpeculativeBoxBoxContacts(double stepSeconds) {
            IReadOnlyList<BodyPair3D> candidatePairs = BroadphaseValue.CollectCandidatePairs(BodyStatesValue);
            LastBroadphaseCandidatePairCount = candidatePairs.Count;
            PrepareBoxBoxContactConstraints();
            for (int iteration = 0; iteration < Settings.SolverIterations; iteration++) {
                for (int pairIndex = 0; pairIndex < candidatePairs.Count; pairIndex++) {
                    BodyPair3D candidatePair = candidatePairs[pairIndex];
                    ResolveBoxBoxPairByMobility(BodyStatesValue[candidatePair.FirstBodyIndex], BodyStatesValue[candidatePair.SecondBodyIndex], stepSeconds, true, false);
                }

                for (int pairIndex = 0; pairIndex < candidatePairs.Count; pairIndex++) {
                    BodyPair3D candidatePair = candidatePairs[pairIndex];
                    ResolveBoxBoxPairByMobility(BodyStatesValue[candidatePair.FirstBodyIndex], BodyStatesValue[candidatePair.SecondBodyIndex], stepSeconds, false, false);
                }
            }

            PruneStaleBoxBoxContactConstraints();
        }

        /// <summary>
        /// Resolves the post-pose contact pass for non-box primitive pairs, static meshes, and trigger overlaps.
        /// </summary>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="processSolidBoxBoxContacts">True when solid box-box contacts should be solved by this pass.</param>
        void ResolveContacts(double stepSeconds, bool processSolidBoxBoxContacts) {
            IReadOnlyList<BodyPair3D> candidatePairs = BroadphaseValue.CollectCandidatePairs(BodyStatesValue);
            LastBroadphaseCandidatePairCount = candidatePairs.Count;
            TriggerEventsValue.Clear();
            CurrentTriggerPairsValue.Clear();
            if (processSolidBoxBoxContacts) {
                PrepareBoxBoxContactConstraints();
            }
            for (int iteration = 0; iteration < Settings.SolverIterations; iteration++) {
                for (int pairIndex = 0; pairIndex < candidatePairs.Count; pairIndex++) {
                    BodyPair3D candidatePair = candidatePairs[pairIndex];
                    ResolvePair(BodyStatesValue[candidatePair.FirstBodyIndex], BodyStatesValue[candidatePair.SecondBodyIndex], iteration == 0, stepSeconds, processSolidBoxBoxContacts);
                }

                ResolveStaticMeshContacts();
            }

            if (processSolidBoxBoxContacts) {
                PruneStaleBoxBoxContactConstraints();
            }
            CollectStaticMeshTriggerOverlaps();
            CollectCharacterControllerTriggerOverlaps();
            FinalizeTriggerEvents();
        }

        /// <summary>
        /// Pushes solver state back into authored entities and rigid bodies.
        /// </summary>
        void SynchronizeToScene() {
            for (int index = 0; index < BodyStatesValue.Count; index++) {
                BodyStatesValue[index].SynchronizeToEntity();
            }

            for (int index = 0; index < ControllerStatesValue.Count; index++) {
                ControllerStatesValue[index].SynchronizeToEntity();
            }
        }

        /// <summary>
        /// Finalizes trigger overlap lifecycle events by comparing the current step pair set against the previously active set.
        /// </summary>
        void FinalizeTriggerEvents() {
            for (int index = 0; index < CurrentTriggerPairsValue.Count; index++) {
                TriggerPairKey3D pairKey = CurrentTriggerPairsValue[index];
                if (ContainsTriggerPair(ActiveTriggerPairsValue, pairKey)) {
                    TriggerEventsValue.Add(CreateTriggerEvent(pairKey, TriggerEventKind3D.Stay));
                } else {
                    TriggerEventsValue.Add(CreateTriggerEvent(pairKey, TriggerEventKind3D.Enter));
                }
            }

            for (int index = 0; index < ActiveTriggerPairsValue.Count; index++) {
                TriggerPairKey3D pairKey = ActiveTriggerPairsValue[index];
                if (!ContainsTriggerPair(CurrentTriggerPairsValue, pairKey)) {
                    TriggerEventsValue.Add(CreateTriggerEvent(pairKey, TriggerEventKind3D.Exit));
                }
            }

            ActiveTriggerPairsValue.Clear();
            for (int index = 0; index < CurrentTriggerPairsValue.Count; index++) {
                ActiveTriggerPairsValue.Add(CurrentTriggerPairsValue[index]);
            }
        }

        /// <summary>
        /// Returns whether the supplied trigger-pair list already contains an equivalent pair key.
        /// </summary>
        /// <param name="pairs">Trigger-pair list to inspect.</param>
        /// <param name="pairKey">Pair key to locate.</param>
        /// <returns>True when the pair is present.</returns>
        static bool ContainsTriggerPair(IReadOnlyList<TriggerPairKey3D> pairs, TriggerPairKey3D pairKey) {
            if (pairs == null) {
                throw new ArgumentNullException(nameof(pairs));
            }

            for (int index = 0; index < pairs.Count; index++) {
                if (pairs[index].Equals(pairKey)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a trigger pair to the current step list when it has not already been tracked.
        /// </summary>
        /// <param name="pairKey">Trigger pair to add.</param>
        void AddCurrentTriggerPair(TriggerPairKey3D pairKey) {
            if (ContainsTriggerPair(CurrentTriggerPairsValue, pairKey)) {
                return;
            }

            CurrentTriggerPairsValue.Add(pairKey);
        }

        /// <summary>
        /// Creates one trigger event from one tracked body pair and lifecycle transition.
        /// </summary>
        /// <param name="pairKey">Tracked trigger overlap pair.</param>
        /// <param name="kind">Lifecycle transition emitted during the current step.</param>
        /// <returns>Trigger event for the supplied pair.</returns>
        TriggerEvent3D CreateTriggerEvent(TriggerPairKey3D pairKey, TriggerEventKind3D kind) {
            return new TriggerEvent3D(kind, pairKey.TriggerEntity, pairKey.OtherEntity);
        }

        /// <summary>
        /// Determines whether two body colliders should be considered for contact or trigger overlap.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <returns>True when the collision layers and masks permit interaction.</returns>
        static bool CanBodiesInteract(BodyState3D first, BodyState3D second) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            return (first.Collider.CollisionMask & second.Collider.CollisionLayer) != 0 &&
                (second.Collider.CollisionMask & first.Collider.CollisionLayer) != 0;
        }

        /// <summary>
        /// Determines whether one dynamic body and one cooked static mesh should be considered for contact.
        /// </summary>
        /// <param name="bodyState">Dynamic body being tested.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        /// <returns>True when the collision layers and masks permit interaction.</returns>
        static bool CanBodyInteractWithStaticMesh(BodyState3D bodyState, StaticMeshBodyState3D meshState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            return (bodyState.Collider.CollisionMask & meshState.MeshCollider.CollisionLayer) != 0 &&
                (meshState.MeshCollider.CollisionMask & bodyState.Collider.CollisionLayer) != 0;
        }

        /// <summary>
        /// Marks every cached box-box contact constraint as untouched before the current collision pass rebuilds active contacts.
        /// </summary>
        void PrepareBoxBoxContactConstraints() {
            for (int index = 0; index < BoxBoxContactConstraintsValue.Count; index++) {
                BoxBoxContactConstraintsValue[index].BeginStep();
            }
        }

        /// <summary>
        /// Removes persistent box-box contact constraints that were not produced by the current collision pass.
        /// </summary>
        void PruneStaleBoxBoxContactConstraints() {
            for (int index = BoxBoxContactConstraintsValue.Count - 1; index >= 0; index--) {
                if (!BoxBoxContactConstraintsValue[index].WasTouchedThisStep) {
                    BoxBoxContactConstraintsValue.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Resolves the persistent box-box contact constraint cache entry for an active body pair.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <returns>Persistent contact constraint cache for the body pair.</returns>
        BoxBoxContactConstraint3D ResolveBoxBoxContactConstraint(BodyState3D first, BodyState3D second) {
            for (int index = 0; index < BoxBoxContactConstraintsValue.Count; index++) {
                BoxBoxContactConstraint3D existingConstraint = BoxBoxContactConstraintsValue[index];
                if (existingConstraint.Matches(first.Entity, second.Entity)) {
                    return existingConstraint;
                }
            }

            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(first.Entity, second.Entity);
            BoxBoxContactConstraintsValue.Add(constraint);
            return constraint;
        }

        /// <summary>
        /// Resolves one pair of overlapping axis-aligned box bodies.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="collectTriggerEvents">True when this pass should collect trigger overlap events for the current step.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="processSolidBoxBoxContacts">True when this pass should solve solid box-box contacts.</param>
        void ResolvePair(BodyState3D first, BodyState3D second, bool collectTriggerEvents, double stepSeconds, bool processSolidBoxBoxContacts) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (!CanBodiesInteract(first, second)) {
                return;
            }
            bool bodiesOverlap = PrimitiveContactMath3D.Overlaps(first, second);
            bool canUseSpeculativeBoxContact = first.ColliderShapeKind == ColliderShapeKind3D.Box &&
                second.ColliderShapeKind == ColliderShapeKind3D.Box;
            if (!bodiesOverlap && !canUseSpeculativeBoxContact) {
                return;
            }
            if (collectTriggerEvents && bodiesOverlap && (first.Collider.IsTrigger || second.Collider.IsTrigger)) {
                TrackTriggerPair(first, second);
            }
            if (first.Collider.IsTrigger || second.Collider.IsTrigger) {
                return;
            }
            if (!CanBeDisplaced(first) && !CanBeDisplaced(second)) {
                return;
            }

            float penetration = 0f;
            float3 collisionNormal = float3.Zero;
            if (first.ColliderShapeKind == ColliderShapeKind3D.Box &&
                second.ColliderShapeKind == ColliderShapeKind3D.Box) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT
                if (!processSolidBoxBoxContacts &&
                    first.RigidBody.BodyKind == BodyKind3D.Dynamic &&
                    second.RigidBody.BodyKind == BodyKind3D.Dynamic) {
                    return;
                }
                if (processSolidBoxBoxContacts || !CanUseAxisAlignedBoxAxisConstraint(first, second)) {
                    ResolveBoxBoxPair(first, second, stepSeconds, true);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Sphere &&
                second.ColliderShapeKind == ColliderShapeKind3D.Sphere) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_SPHERE_CONTACT
                if (SphereSphereContactResolver3D.TryResolveContact(first, second, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(first, second, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Sphere &&
                second.ColliderShapeKind == ColliderShapeKind3D.Box) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_BOX_CONTACT
                if (SphereBoxContactResolver3D.TryResolveContact(first, second, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(first, second, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Box &&
                second.ColliderShapeKind == ColliderShapeKind3D.Sphere) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_BOX_CONTACT
                if (SphereBoxContactResolver3D.TryResolveContact(second, first, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(second, first, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Capsule &&
                second.ColliderShapeKind == ColliderShapeKind3D.Box) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_BOX_CONTACT
                if (CapsuleBoxContactResolver3D.TryResolveContact(first, second, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(first, second, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Box &&
                second.ColliderShapeKind == ColliderShapeKind3D.Capsule) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_BOX_CONTACT
                if (CapsuleBoxContactResolver3D.TryResolveContact(second, first, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(second, first, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Capsule &&
                second.ColliderShapeKind == ColliderShapeKind3D.Sphere) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_SPHERE_CONTACT
                if (CapsuleSphereContactResolver3D.TryResolveContact(first, second, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(first, second, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Sphere &&
                second.ColliderShapeKind == ColliderShapeKind3D.Capsule) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_SPHERE_CONTACT
                if (CapsuleSphereContactResolver3D.TryResolveContact(second, first, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(second, first, collisionNormal, penetration);
                }
#endif
                return;
            }
            if (first.ColliderShapeKind == ColliderShapeKind3D.Capsule &&
                second.ColliderShapeKind == ColliderShapeKind3D.Capsule) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_CAPSULE_CONTACT
                if (CapsuleCapsuleContactResolver3D.TryResolveContact(first, second, out collisionNormal, out penetration)) {
                    ResolvePairAlongNormal(first, second, collisionNormal, penetration);
                }
#endif
                return;
            }

            throw new InvalidOperationException($"Unsupported collider pair '{first.ColliderShapeKind}' and '{second.ColliderShapeKind}'.");
        }

        /// <summary>
        /// Resolves one solid box-box pair using speculative contact distance and persistent contact impulses.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="applyPositionCorrection">True when direct projection should be used after pose integration.</param>
        void ResolveBoxBoxPair(BodyState3D first, BodyState3D second, double stepSeconds, bool applyPositionCorrection) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_BOX_CONTACT
            if (first.ColliderShapeKind != ColliderShapeKind3D.Box || second.ColliderShapeKind != ColliderShapeKind3D.Box) {
                return;
            }
            if (first.Collider.IsTrigger || second.Collider.IsTrigger) {
                return;
            }
            if (!CanBeDisplaced(first) && !CanBeDisplaced(second)) {
                return;
            }
            if (applyPositionCorrection && IsDynamicDynamicPair(first, second)) {
                return;
            }

            float speculativeContactMargin = ResolveSpeculativeContactMargin(first, second, stepSeconds);
            if (BoxBoxContactResolver3D.TryResolveManifold(first, second, speculativeContactMargin, out BoxBoxContactManifold3D manifold)) {
                ResolveBoxBoxManifold(first, second, manifold, stepSeconds, applyPositionCorrection);
                return;
            }
            if (IsDynamicDynamicPair(first, second) &&
                BoxBoxContactResolver3D.TryResolveOrientedManifold(first, second, speculativeContactMargin, out BoxBoxContactManifold3D orientedManifold)) {
                ResolveBoxBoxManifold(first, second, orientedManifold, stepSeconds, applyPositionCorrection);
                return;
            }
            if (BoxBoxContactResolver3D.TryResolveContact(first, second, speculativeContactMargin, out float penetration, out int axisIndex)) {
                ResolveAxis(first, second, penetration, axisIndex, stepSeconds, applyPositionCorrection);
            }
#endif
        }

        /// <summary>
        /// Resolves one box-box pair only when it belongs to the requested dynamic-dynamic or support-contact phase.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="resolveDynamicPairs">True for dynamic-dynamic pairs; false for pairs involving a static or kinematic support.</param>
        /// <param name="applyPositionCorrection">True when direct projection should be used for legacy post-pose contacts.</param>
        void ResolveBoxBoxPairByMobility(BodyState3D first, BodyState3D second, double stepSeconds, bool resolveDynamicPairs, bool applyPositionCorrection) {
            bool isDynamicPair = first.RigidBody.BodyKind == BodyKind3D.Dynamic && second.RigidBody.BodyKind == BodyKind3D.Dynamic;
            if (isDynamicPair != resolveDynamicPairs) {
                return;
            }

            ResolveBoxBoxPair(first, second, stepSeconds, applyPositionCorrection);
        }

        /// <summary>
        /// Applies separation and distributed velocity response for one box-box contact manifold.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="manifold">Resolved box-box contact manifold.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="applyPositionCorrection">True when direct projection should be used after pose integration.</param>
        void ResolveBoxBoxManifold(BodyState3D first, BodyState3D second, BoxBoxContactManifold3D manifold, double stepSeconds, bool applyPositionCorrection) {
            bool canMoveFirst = CanBeDisplaced(first);
            bool canMoveSecond = CanBeDisplaced(second);
            float correctionPenetration = applyPositionCorrection ? Math.Max(0f, manifold.Penetration) : 0f;
            float moveFirst = 0f;
            float moveSecond = 0f;
            float3 firstCorrection = float3.Zero;
            float3 secondCorrection = float3.Zero;

            if (canMoveFirst && canMoveSecond) {
                moveFirst = correctionPenetration * 0.5f;
                moveSecond = correctionPenetration * 0.5f;
            } else if (canMoveFirst) {
                moveFirst = correctionPenetration;
            } else if (canMoveSecond) {
                moveSecond = correctionPenetration;
            }

            if (moveFirst != 0f) {
                firstCorrection = manifold.Normal * moveFirst;
                first.Position = first.Position + firstCorrection;
            }

            if (moveSecond != 0f) {
                secondCorrection = manifold.Normal * (moveSecond * -1f);
                second.Position = second.Position + secondCorrection;
            }

            if (manifold.ContactCount > 0) {
                if (!applyPositionCorrection || !IsDynamicDynamicPair(first, second)) {
                    ContactMaterialResponse3D.ApplyBoxBoxConstraintResponse(first, second, manifold, ResolveBoxBoxContactConstraint(first, second), stepSeconds);
                }
                first.ContactWasResolvedThisStep = true;
                second.ContactWasResolvedThisStep = true;
                RecordContactNormal(first, manifold.Normal);
                RecordContactNormal(second, manifold.Normal * -1f);
                RecordBoxBoxSupportStability(first, second, manifold.Normal);
            }

            ClipVelocityAgainstCorrection(first, firstCorrection);
            ClipVelocityAgainstCorrection(second, secondCorrection);
        }

        /// <summary>
        /// Applies separation and velocity clipping for one collision axis.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="penetration">Positive overlap distance on the selected axis.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <param name="applyPositionCorrection">True when direct projection should be used after pose integration.</param>
        void ResolveAxis(BodyState3D first, BodyState3D second, float penetration, int axisIndex, double stepSeconds, bool applyPositionCorrection) {
            if (penetration <= 0f) {
                return;
            }
            if (CanUseAxisAlignedBoxAxisConstraint(first, second)) {
                ResolveBoxBoxManifold(first, second, CreateAxisBoxBoxManifold(first, second, penetration, axisIndex), stepSeconds, applyPositionCorrection);
                return;
            }
            if (!applyPositionCorrection) {
                ContactMaterialResponse3D.ApplyAxisPairResponse(first, second, axisIndex, PrimitiveContactMath3D.GetAxisDirection(first, second, axisIndex));
                return;
            }

            float axisDirection = PrimitiveContactMath3D.GetAxisDirection(first, second, axisIndex);
            bool canMoveFirst = CanBeDisplaced(first);
            bool canMoveSecond = CanBeDisplaced(second);
            float moveFirst = 0f;
            float moveSecond = 0f;
            float3 firstCorrection = float3.Zero;
            float3 secondCorrection = float3.Zero;

            if (canMoveFirst && canMoveSecond) {
                moveFirst = penetration * 0.5f * axisDirection;
                moveSecond = penetration * 0.5f * -axisDirection;
            } else if (canMoveFirst) {
                moveFirst = penetration * axisDirection;
            } else if (canMoveSecond) {
                moveSecond = penetration * -axisDirection;
            }

            if (moveFirst != 0f) {
                first.Position = PrimitiveContactMath3D.OffsetAxis(first.Position, axisIndex, moveFirst);
                firstCorrection = CreateAxisNormal(axisIndex, moveFirst > 0f ? 1f : -1f) * Math.Abs(moveFirst);
            }

            if (moveSecond != 0f) {
                second.Position = PrimitiveContactMath3D.OffsetAxis(second.Position, axisIndex, moveSecond);
                secondCorrection = CreateAxisNormal(axisIndex, moveSecond > 0f ? 1f : -1f) * Math.Abs(moveSecond);
            }

            if (moveFirst != 0f || moveSecond != 0f) {
                float3 collisionNormal = CreateAxisNormal(axisIndex, axisDirection);
                if (!IsDynamicDynamicPair(first, second)) {
                    ContactMaterialResponse3D.ApplyAxisPairResponse(first, second, axisIndex, axisDirection);
                }
                first.ContactWasResolvedThisStep = true;
                second.ContactWasResolvedThisStep = true;
                RecordContactNormal(first, collisionNormal);
                RecordContactNormal(second, collisionNormal * -1f);
                RecordBoxBoxSupportStability(first, second, collisionNormal);
            }

            ClipVelocityAgainstCorrection(first, firstCorrection);
            ClipVelocityAgainstCorrection(second, secondCorrection);
        }

        /// <summary>
        /// Returns whether a one-point axis constraint can use axis-aligned box overlap coordinates instead of oriented support points.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <returns>True when both boxes are upright enough for an axis-aligned persistent contact feature.</returns>
        static bool CanUseAxisAlignedBoxAxisConstraint(BodyState3D first, BodyState3D second) {
            if (first.ColliderShapeKind != ColliderShapeKind3D.Box || second.ColliderShapeKind != ColliderShapeKind3D.Box) {
                return false;
            }

            return IsAxisAlignedBoxConstraintCandidate(first) && IsAxisAlignedBoxConstraintCandidate(second);
        }

        /// <summary>
        /// Returns whether both bodies are dynamic rigid bodies whose velocity response should already be handled by the speculative solver.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <returns>True when both bodies are dynamic.</returns>
        static bool IsDynamicDynamicPair(BodyState3D first, BodyState3D second) {
            return first.RigidBody.BodyKind == BodyKind3D.Dynamic &&
                second.RigidBody.BodyKind == BodyKind3D.Dynamic;
        }

        /// <summary>
        /// Removes the velocity component that would immediately push a corrected dynamic body back into the resolved contact.
        /// </summary>
        /// <param name="bodyState">Body that received a positional correction.</param>
        /// <param name="correction">World-space correction applied to the body position.</param>
        static void ClipVelocityAgainstCorrection(BodyState3D bodyState, float3 correction) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            double correctionLengthSquared = float3.Dot(correction, correction);
            if (correctionLengthSquared <= 0.0000001d) {
                return;
            }

            float3 correctionNormal = correction / (float)Math.Sqrt(correctionLengthSquared);
            float velocityTowardContact = float3.Dot(bodyState.Velocity, correctionNormal);
            if (velocityTowardContact < 0f) {
                bodyState.Velocity = bodyState.Velocity - (correctionNormal * velocityTowardContact);
            }
        }

        /// <summary>
        /// Returns whether one box is upright enough for the one-point axis constraint to use its axis-aligned bounds.
        /// </summary>
        /// <param name="bodyState">Box body state to inspect.</param>
        /// <returns>True when the local up axis is close to world up.</returns>
        static bool IsAxisAlignedBoxConstraintCandidate(BodyState3D bodyState) {
            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            return up.Y >= RestingUprightCandidateYThreshold;
        }

        /// <summary>
        /// Resolves a BEPU-style speculative contact margin from relative body speed over the current fixed step.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Speculative contact margin for the current pair.</returns>
        static float ResolveSpeculativeContactMargin(BodyState3D first, BodyState3D second, double stepSeconds) {
            float3 relativeVelocity = first.Velocity - second.Velocity;
            double relativeSpeedSquared = (relativeVelocity.X * relativeVelocity.X) +
                (relativeVelocity.Y * relativeVelocity.Y) +
                (relativeVelocity.Z * relativeVelocity.Z);
            float velocityMargin = (float)(Math.Sqrt(relativeSpeedSquared) * stepSeconds);
            return Math.Max(MinimumSpeculativeContactMargin, velocityMargin);
        }

        /// <summary>
        /// Builds a one-point manifold for an upright box-box contact whose overlap is best represented by one separating axis.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="penetration">Positive overlap distance on the selected axis.</param>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>One-point box-box manifold.</returns>
        static BoxBoxContactManifold3D CreateAxisBoxBoxManifold(BodyState3D first, BodyState3D second, float penetration, int axisIndex) {
            float axisDirection = PrimitiveContactMath3D.GetAxisDirection(first, second, axisIndex);
            float3 normal = CreateAxisNormal(axisIndex, axisDirection);
            return new BoxBoxContactManifold3D {
                Normal = normal,
                Penetration = penetration,
                Penetration0 = penetration,
                Contact0 = CreateAxisBoxBoxContactPoint(first, second, normal, axisIndex),
                FeatureId0 = 100 + axisIndex,
                ContactCount = 1
            };
        }

        /// <summary>
        /// Creates one contact point on the selected axis and centered inside the tangent overlap patch.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="normal">Contact normal pointing from the second box toward the first box.</param>
        /// <param name="axisIndex">Normal axis index.</param>
        /// <returns>World-space contact point.</returns>
        static float3 CreateAxisBoxBoxContactPoint(BodyState3D first, BodyState3D second, float3 normal, int axisIndex) {
            float x = ResolveAxisBoxBoxContactComponent(first, second, normal, axisIndex, 0);
            float y = ResolveAxisBoxBoxContactComponent(first, second, normal, axisIndex, 1);
            float z = ResolveAxisBoxBoxContactComponent(first, second, normal, axisIndex, 2);
            return new float3(x, y, z);
        }

        /// <summary>
        /// Resolves one component of an axis manifold contact point.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="normal">Contact normal pointing from the second box toward the first box.</param>
        /// <param name="axisIndex">Normal axis index.</param>
        /// <param name="componentIndex">Component index to resolve.</param>
        /// <returns>Resolved contact coordinate.</returns>
        static float ResolveAxisBoxBoxContactComponent(BodyState3D first, BodyState3D second, float3 normal, int axisIndex, int componentIndex) {
            if (componentIndex == axisIndex) {
                float normalComponent = ReadComponent(normal, componentIndex);
                float firstCenter = ReadComponent(first.Position, componentIndex);
                float firstExtent = ReadComponent(first.AxisAlignedHalfExtents, componentIndex);
                return normalComponent >= 0f
                    ? firstCenter - firstExtent
                    : firstCenter + firstExtent;
            }

            float firstMinimum = ReadComponent(first.Position, componentIndex) - ReadComponent(first.AxisAlignedHalfExtents, componentIndex);
            float firstMaximum = ReadComponent(first.Position, componentIndex) + ReadComponent(first.AxisAlignedHalfExtents, componentIndex);
            float secondMinimum = ReadComponent(second.Position, componentIndex) - ReadComponent(second.AxisAlignedHalfExtents, componentIndex);
            float secondMaximum = ReadComponent(second.Position, componentIndex) + ReadComponent(second.AxisAlignedHalfExtents, componentIndex);
            return (Math.Max(firstMinimum, secondMinimum) + Math.Min(firstMaximum, secondMaximum)) * 0.5f;
        }

        /// <summary>
        /// Reads one vector component by numeric index.
        /// </summary>
        /// <param name="value">Vector to inspect.</param>
        /// <param name="componentIndex">Zero for X, one for Y, two for Z.</param>
        /// <returns>Selected vector component.</returns>
        static float ReadComponent(float3 value, int componentIndex) {
            if (componentIndex == 0) {
                return value.X;
            }
            if (componentIndex == 1) {
                return value.Y;
            }
            if (componentIndex == 2) {
                return value.Z;
            }

            throw new ArgumentOutOfRangeException(nameof(componentIndex), "Component index must be between zero and two.");
        }

        /// <summary>
        /// Applies separation and velocity clipping for one collision normal.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="collisionNormal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="penetration">Positive overlap distance along the supplied normal.</param>
        void ResolvePairAlongNormal(BodyState3D first, BodyState3D second, float3 collisionNormal, float penetration) {
            bool canMoveFirst = CanBeDisplaced(first);
            bool canMoveSecond = CanBeDisplaced(second);
            float moveFirst = 0f;
            float moveSecond = 0f;
            float3 firstCorrection = float3.Zero;
            float3 secondCorrection = float3.Zero;

            if (canMoveFirst && canMoveSecond) {
                moveFirst = penetration * 0.5f;
                moveSecond = penetration * 0.5f;
            } else if (canMoveFirst) {
                moveFirst = penetration;
            } else if (canMoveSecond) {
                moveSecond = penetration;
            }

            if (moveFirst != 0f) {
                firstCorrection = collisionNormal * moveFirst;
                first.Position = first.Position + firstCorrection;
            }

            if (moveSecond != 0f) {
                secondCorrection = collisionNormal * (moveSecond * -1f);
                second.Position = second.Position + secondCorrection;
            }

            if (moveFirst != 0f || moveSecond != 0f) {
                ContactMaterialResponse3D.ApplyPairResponse(first, second, collisionNormal);
                first.ContactWasResolvedThisStep = true;
                second.ContactWasResolvedThisStep = true;
                RecordContactNormal(first, collisionNormal);
                RecordContactNormal(second, collisionNormal * -1f);
            }

            if (!IsDynamicDynamicPair(first, second)) {
                ClipVelocityAgainstCorrection(first, firstCorrection);
                ClipVelocityAgainstCorrection(second, secondCorrection);
            }
        }

        /// <summary>
        /// Applies static-mesh contact resolution to every supported dynamic body against every cooked mesh in the world.
        /// </summary>
        void ResolveStaticMeshContacts() {
            for (int bodyIndex = 0; bodyIndex < BodyStatesValue.Count; bodyIndex++) {
                BodyState3D bodyState = BodyStatesValue[bodyIndex];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                for (int meshIndex = 0; meshIndex < StaticMeshStatesValue.Count; meshIndex++) {
                    StaticMeshBodyState3D meshState = StaticMeshStatesValue[meshIndex];
                    if (!CanBodyInteractWithStaticMesh(bodyState, meshState)) {
                        continue;
                    }

                    ResolveStaticMeshPair(bodyState, meshState);
                }
            }
        }

        /// <summary>
        /// Collects trigger overlaps between primitive bodies and cooked static meshes after the solver positions have settled.
        /// </summary>
        void CollectStaticMeshTriggerOverlaps() {
            for (int bodyIndex = 0; bodyIndex < BodyStatesValue.Count; bodyIndex++) {
                BodyState3D bodyState = BodyStatesValue[bodyIndex];
                for (int meshIndex = 0; meshIndex < StaticMeshStatesValue.Count; meshIndex++) {
                    StaticMeshBodyState3D meshState = StaticMeshStatesValue[meshIndex];
                    if (!CanBodyInteractWithStaticMesh(bodyState, meshState)) {
                        continue;
                    }
                    if (!bodyState.Collider.IsTrigger && !meshState.MeshCollider.IsTrigger) {
                        continue;
                    }
                    if (!StaticMeshTriggerResolver3D.TryResolveOverlap(bodyState, meshState)) {
                        continue;
                    }

                    TrackTriggerPair(bodyState, meshState);
                }
            }
        }

        /// <summary>
        /// Collects trigger overlaps between character controllers and trigger rigid bodies after controller motion has completed.
        /// </summary>
        void CollectCharacterControllerTriggerOverlaps() {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER
            for (int controllerIndex = 0; controllerIndex < ControllerStatesValue.Count; controllerIndex++) {
                CharacterControllerState3D controllerState = ControllerStatesValue[controllerIndex];
                CharacterControllerBodyTriggerResolver3D.CollectOverlaps(controllerState, BodyStatesValue, CurrentTriggerPairsValue);
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT
                CharacterControllerStaticMeshTriggerResolver3D.CollectOverlaps(controllerState, StaticMeshStatesValue, CurrentTriggerPairsValue);
#endif
            }
#endif
        }

        /// <summary>
        /// Resolves one supported dynamic-body pair against one cooked static mesh.
        /// </summary>
        /// <param name="bodyState">Dynamic body being resolved.</param>
        /// <param name="meshState">Cooked static mesh being tested.</param>
        void ResolveStaticMeshPair(BodyState3D bodyState, StaticMeshBodyState3D meshState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }
            if (bodyState.Collider.IsTrigger || meshState.MeshCollider.IsTrigger) {
                return;
            }

            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Sphere) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_SPHERE_STATIC_MESH_CONTACT
                float3 sphereCollisionNormal = float3.Zero;
                float spherePenetration = 0f;
                if (SphereStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out sphereCollisionNormal, out spherePenetration)) {
                    ResolveDynamicBodyAlongNormal(bodyState, ResolveStaticSurfaceCollider(meshState), sphereCollisionNormal, spherePenetration);
                }
#endif
                return;
            }
            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Capsule) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CAPSULE_STATIC_MESH_CONTACT
                float3 capsuleCollisionNormal = float3.Zero;
                float capsulePenetration = 0f;
                if (CapsuleStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out capsuleCollisionNormal, out capsulePenetration)) {
                    ResolveDynamicBodyAlongNormal(bodyState, ResolveStaticSurfaceCollider(meshState), capsuleCollisionNormal, capsulePenetration);
                }
#endif
                return;
            }
            if (bodyState.ColliderShapeKind == ColliderShapeKind3D.Box) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_BOX_STATIC_MESH_CONTACT
                float3 boxCollisionNormal = float3.Zero;
                float boxPenetration = 0f;
                if (BoxStaticMeshContactResolver3D.TryResolveContact(bodyState, meshState, out boxCollisionNormal, out boxPenetration)) {
                    ResolveDynamicBodyAlongNormal(bodyState, ResolveStaticSurfaceCollider(meshState), boxCollisionNormal, boxPenetration);
                }
#endif
            }
        }

        /// <summary>
        /// Applies separation and velocity clipping for one dynamic body against an immovable collision normal.
        /// </summary>
        /// <param name="bodyState">Dynamic body being corrected.</param>
        /// <param name="surfaceCollider">Static collider providing friction and restitution values.</param>
        /// <param name="collisionNormal">Unit collision normal pointing away from the static surface.</param>
        /// <param name="penetration">Positive overlap depth.</param>
        void ResolveDynamicBodyAlongNormal(BodyState3D bodyState, Collider3DComponent surfaceCollider, float3 collisionNormal, float penetration) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (surfaceCollider == null) {
                throw new ArgumentNullException(nameof(surfaceCollider));
            }

            if (!CanBeDisplaced(bodyState) || penetration <= 0f) {
                return;
            }

            float3 correction = collisionNormal * penetration;
            bodyState.Position = bodyState.Position + correction;
            ContactMaterialResponse3D.ApplyStaticSurfaceResponse(bodyState, surfaceCollider, collisionNormal);
            ClipVelocityAgainstCorrection(bodyState, correction);
            bodyState.ContactWasResolvedThisStep = true;
            RecordContactNormal(bodyState, collisionNormal);
        }

        /// <summary>
        /// Records the strongest upward-facing contact normal seen by one body this step.
        /// </summary>
        /// <param name="bodyState">Body that received the contact normal.</param>
        /// <param name="contactNormal">Contact normal pointing away from the opposing surface.</param>
        static void RecordContactNormal(BodyState3D bodyState, float3 contactNormal) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (contactNormal.Y > bodyState.MaximumContactNormalY) {
                bodyState.MaximumContactNormalY = contactNormal.Y;
            }
        }

        /// <summary>
        /// Marks dynamic boxes that are balanced outside the footprint of their supporting box contact.
        /// </summary>
        /// <param name="first">First box body state.</param>
        /// <param name="second">Second box body state.</param>
        /// <param name="firstContactNormal">Contact normal pointing away from the second body and into the first body.</param>
        static void RecordBoxBoxSupportStability(BodyState3D first, BodyState3D second, float3 firstContactNormal) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }
            if (first.RigidBody.BodyKind == BodyKind3D.Dynamic && second.RigidBody.BodyKind == BodyKind3D.Dynamic) {
                return;
            }

            if (firstContactNormal.Y > 0.6f) {
                RecordBoxSupportStability(first, second);
                return;
            }
            if (firstContactNormal.Y < -0.6f) {
                RecordBoxSupportStability(second, first);
            }
        }

        /// <summary>
        /// Marks one supported dynamic box as unstable when its center does not project into the support footprint.
        /// </summary>
        /// <param name="supportedBody">Dynamic body resting above the support body.</param>
        /// <param name="supportBody">Body providing the upward support contact.</param>
        static void RecordBoxSupportStability(BodyState3D supportedBody, BodyState3D supportBody) {
            if (supportedBody == null) {
                throw new ArgumentNullException(nameof(supportedBody));
            }
            if (supportBody == null) {
                throw new ArgumentNullException(nameof(supportBody));
            }
            if (supportedBody.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                return;
            }

            if (IsCenterOutsideSupportFootprint(supportedBody, supportBody)) {
                supportedBody.HasUnstableSupportContactThisStep = true;
            } else {
                supportedBody.HasStableSupportContactThisStep = true;
            }
        }

        /// <summary>
        /// Determines whether a supported body's center sits outside the horizontal bounds of the supporting box.
        /// </summary>
        /// <param name="supportedBody">Body being supported.</param>
        /// <param name="supportBody">Box body providing support.</param>
        /// <returns>True when the supported center projects outside the support footprint.</returns>
        static bool IsCenterOutsideSupportFootprint(BodyState3D supportedBody, BodyState3D supportBody) {
            if (supportedBody == null) {
                throw new ArgumentNullException(nameof(supportedBody));
            }
            if (supportBody == null) {
                throw new ArgumentNullException(nameof(supportBody));
            }

            float supportMinimumX = supportBody.Position.X - supportBody.AxisAlignedHalfExtents.X;
            float supportMaximumX = supportBody.Position.X + supportBody.AxisAlignedHalfExtents.X;
            float supportMinimumZ = supportBody.Position.Z - supportBody.AxisAlignedHalfExtents.Z;
            float supportMaximumZ = supportBody.Position.Z + supportBody.AxisAlignedHalfExtents.Z;
            return supportedBody.Position.X < supportMinimumX ||
                supportedBody.Position.X > supportMaximumX ||
                supportedBody.Position.Z < supportMinimumZ ||
                supportedBody.Position.Z > supportMaximumZ;
        }

        /// <summary>
        /// Builds one axis-aligned normal from a solver axis index and sign.
        /// </summary>
        /// <param name="axisIndex">Zero for X, one for Y, two for Z.</param>
        /// <param name="axisDirection">Signed direction along the selected axis.</param>
        /// <returns>Axis-aligned unit normal.</returns>
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

        /// <summary>
        /// Determines whether a body is close enough to upright for rest stabilization to remove tiny angular noise.
        /// </summary>
        /// <param name="bodyState">Body state whose current orientation should be inspected.</param>
        /// <returns>True when the body is a box and its local up axis is close to world up.</returns>
        static bool IsRestingUprightCandidate(BodyState3D bodyState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (bodyState.ColliderShapeKind != ColliderShapeKind3D.Box) {
                return false;
            }

            return ResolveBodyUpY(bodyState) >= RestingUprightCandidateYThreshold;
        }

        /// <summary>
        /// Resolves the world Y component of a box body's local up axis.
        /// </summary>
        /// <param name="bodyState">Body state whose up axis should be inspected.</param>
        /// <returns>World Y component of the local up axis.</returns>
        static float ResolveBodyUpY(BodyState3D bodyState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), bodyState.Orientation);
            return up.Y;
        }

        /// <summary>
        /// Resolves the authored collider that should provide one static-surface material response.
        /// </summary>
        /// <param name="meshState">Static mesh state that received the contact query.</param>
        /// <returns>Static surface collider used for material response.</returns>
        static Collider3DComponent ResolveStaticSurfaceCollider(StaticMeshBodyState3D meshState) {
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            return meshState.MeshCollider;
        }

        /// <summary>
        /// Tracks one primitive body trigger pair using the entity that owns the trigger collider.
        /// </summary>
        /// <param name="first">First body participating in the overlap.</param>
        /// <param name="second">Second body participating in the overlap.</param>
        void TrackTriggerPair(BodyState3D first, BodyState3D second) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            if (first.Collider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(first.Entity, second.Entity));
                return;
            }
            if (second.Collider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(second.Entity, first.Entity));
                return;
            }

            throw new InvalidOperationException("Tracked trigger overlap pair does not contain a trigger collider.");
        }

        /// <summary>
        /// Tracks one primitive-body to static-mesh trigger pair using whichever collider is configured as the trigger.
        /// </summary>
        /// <param name="bodyState">Primitive body participating in the overlap.</param>
        /// <param name="meshState">Static mesh participating in the overlap.</param>
        void TrackTriggerPair(BodyState3D bodyState, StaticMeshBodyState3D meshState) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (meshState == null) {
                throw new ArgumentNullException(nameof(meshState));
            }

            if (bodyState.Collider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(bodyState.Entity, meshState.Entity));
                return;
            }
            if (meshState.MeshCollider.IsTrigger) {
                AddCurrentTriggerPair(new TriggerPairKey3D(meshState.Entity, bodyState.Entity));
                return;
            }

            throw new InvalidOperationException("Tracked trigger overlap pair does not contain a trigger collider.");
        }

        /// <summary>
        /// Collects supported rigid-body entities recursively from one scene subtree.
        /// </summary>
        /// <param name="entity">Current scene entity.</param>
        void CollectBodyStates(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            RigidBody3DComponent rigidBody = FindRigidBody(entity);
            BoxCollider3DComponent boxCollider = FindBoxCollider(entity);
            SphereCollider3DComponent sphereCollider = FindSphereCollider(entity);
            CapsuleCollider3DComponent capsuleCollider = FindCapsuleCollider(entity);
            StaticMeshCollider3DComponent staticMeshCollider = FindStaticMeshCollider(entity);
            CharacterController3DComponent characterController = FindCharacterController(entity);
            if (rigidBody != null && characterController != null) {
                throw new InvalidOperationException("Entities cannot bind both a rigid body and a character controller at the same time.");
            }
            if (CountNonNullColliders(boxCollider, sphereCollider, capsuleCollider, staticMeshCollider) > 1) {
                throw new InvalidOperationException("Rigid-body entities cannot bind more than one collider shape at the same time.");
            }
            if (rigidBody != null && boxCollider != null) {
                BodyStatesValue.Add(new BodyState3D(entity, rigidBody, boxCollider, FindKinematicMotion(entity)));
            } else if (rigidBody != null && sphereCollider != null) {
                BodyStatesValue.Add(new BodyState3D(entity, rigidBody, sphereCollider, FindKinematicMotion(entity)));
            } else if (rigidBody != null && capsuleCollider != null) {
                BodyStatesValue.Add(new BodyState3D(entity, rigidBody, capsuleCollider, FindKinematicMotion(entity)));
            } else if (rigidBody != null && staticMeshCollider != null) {
                if (rigidBody.BodyKind != BodyKind3D.Static) {
                    throw new InvalidOperationException("Static mesh colliders currently require a static rigid body.");
                }

                StaticMeshStatesValue.Add(new StaticMeshBodyState3D(entity, rigidBody, staticMeshCollider));
            }
            if (characterController != null && boxCollider != null) {
                ControllerStatesValue.Add(new CharacterControllerState3D(entity, characterController, boxCollider));
            } else if (characterController != null && (sphereCollider != null || capsuleCollider != null)) {
                throw new InvalidOperationException("Character controllers currently require a box collider.");
            }

            if (entity.Children == null) {
                return;
            }

            for (int index = 0; index < entity.Children.Count; index++) {
                CollectBodyStates(entity.Children[index]);
            }
        }

        /// <summary>
        /// Resolves the rigid body component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose rigid body should be found.</param>
        /// <returns>Attached rigid body when present; otherwise null.</returns>
        static RigidBody3DComponent FindRigidBody(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is RigidBody3DComponent rigidBody) {
                    return rigidBody;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the box collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose box collider should be found.</param>
        /// <returns>Attached box collider when present; otherwise null.</returns>
        static BoxCollider3DComponent FindBoxCollider(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BoxCollider3DComponent boxCollider) {
                    return boxCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the sphere collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose sphere collider should be found.</param>
        /// <returns>Attached sphere collider when present; otherwise null.</returns>
        static SphereCollider3DComponent FindSphereCollider(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is SphereCollider3DComponent sphereCollider) {
                    return sphereCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the capsule collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose capsule collider should be found.</param>
        /// <returns>Attached capsule collider when present; otherwise null.</returns>
        static CapsuleCollider3DComponent FindCapsuleCollider(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is CapsuleCollider3DComponent capsuleCollider) {
                    return capsuleCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the static mesh collider component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose static mesh collider should be found.</param>
        /// <returns>Attached static mesh collider when present; otherwise null.</returns>
        static StaticMeshCollider3DComponent FindStaticMeshCollider(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is StaticMeshCollider3DComponent staticMeshCollider) {
                    return staticMeshCollider;
                }
            }

            return null;
        }

        /// <summary>
        /// Counts the number of non-null collider references supplied for one entity.
        /// </summary>
        /// <param name="boxCollider">Box collider reference.</param>
        /// <param name="sphereCollider">Sphere collider reference.</param>
        /// <param name="capsuleCollider">Capsule collider reference.</param>
        /// <returns>Number of supplied collider references that are not null.</returns>
        static int CountNonNullColliders(BoxCollider3DComponent boxCollider, SphereCollider3DComponent sphereCollider, CapsuleCollider3DComponent capsuleCollider, StaticMeshCollider3DComponent staticMeshCollider) {
            int count = 0;
            if (boxCollider != null) {
                count++;
            }
            if (sphereCollider != null) {
                count++;
            }
            if (capsuleCollider != null) {
                count++;
            }
            if (staticMeshCollider != null) {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Resolves the character-controller component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose character-controller component should be found.</param>
        /// <returns>Attached character-controller component when present; otherwise null.</returns>
        static CharacterController3DComponent FindCharacterController(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is CharacterController3DComponent controllerComponent) {
                    return controllerComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the body can be translated by the solver.
        /// </summary>
        /// <param name="bodyState">Body state to test.</param>
        /// <returns>True when the body is kinematic or dynamic.</returns>
        static bool CanBeDisplaced(BodyState3D bodyState) {
            return bodyState.RigidBody.BodyKind == BodyKind3D.Dynamic;
        }

        /// <summary>
        /// Creates the broadphase implementation requested by the effective world settings.
        /// </summary>
        /// <param name="settings">Effective world settings.</param>
        /// <returns>Broadphase implementation for candidate-pair generation.</returns>
        static IBroadphase3D CreateBroadphase(PhysicsWorld3DSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.BroadphaseKind == BroadphaseKind3D.UniformGrid) {
                return new UniformGridBroadphase3D(4d);
            }
            if (settings.BroadphaseKind == BroadphaseKind3D.SweepAndPrune) {
                throw new NotSupportedException("Sweep-and-prune broadphase is not implemented yet.");
            }

            throw new InvalidOperationException($"Unsupported broadphase kind '{settings.BroadphaseKind}'.");
        }

        /// <summary>
        /// Resolves the kinematic motion component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity whose kinematic motion component should be found.</param>
        /// <returns>Attached kinematic motion component when present; otherwise null.</returns>
        static KinematicMotion3DComponent FindKinematicMotion(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is KinematicMotion3DComponent kinematicMotion) {
                    return kinematicMotion;
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluates one kinematic motion path at the supplied elapsed time.
        /// </summary>
        /// <param name="motionComponent">Authored motion path component.</param>
        /// <param name="elapsedSeconds">Accumulated runtime elapsed time for the path.</param>
        /// <returns>Evaluated path position.</returns>
        static float3 EvaluateKinematicMotionPosition(KinematicMotion3DComponent motionComponent, double elapsedSeconds) {
            if (motionComponent == null) {
                throw new ArgumentNullException(nameof(motionComponent));
            }

            double normalizedProgress = CalculateNormalizedMotionProgress(motionComponent, elapsedSeconds);
            float progress = (float)normalizedProgress;
            float3 start = motionComponent.StartLocalPosition;
            float3 end = motionComponent.EndLocalPosition;
            return new float3(
                start.X + ((end.X - start.X) * progress),
                start.Y + ((end.Y - start.Y) * progress),
                start.Z + ((end.Z - start.Z) * progress));
        }

        /// <summary>
        /// Calculates the normalized zero-to-one progress for one kinematic motion path.
        /// </summary>
        /// <param name="motionComponent">Authored motion path component.</param>
        /// <param name="elapsedSeconds">Accumulated runtime elapsed time for the path.</param>
        /// <returns>Normalized progress along the path.</returns>
        static double CalculateNormalizedMotionProgress(KinematicMotion3DComponent motionComponent, double elapsedSeconds) {
            if (motionComponent == null) {
                throw new ArgumentNullException(nameof(motionComponent));
            }

            if (motionComponent.PingPong) {
                double cycleSeconds = motionComponent.TravelDurationSeconds * 2d;
                double wrappedSeconds = elapsedSeconds % cycleSeconds;
                if (wrappedSeconds < 0d) {
                    wrappedSeconds += cycleSeconds;
                }
                if (wrappedSeconds <= motionComponent.TravelDurationSeconds) {
                    return wrappedSeconds / motionComponent.TravelDurationSeconds;
                }

                return 1d - ((wrappedSeconds - motionComponent.TravelDurationSeconds) / motionComponent.TravelDurationSeconds);
            }

            if (elapsedSeconds <= 0d) {
                return 0d;
            }
            if (elapsedSeconds >= motionComponent.TravelDurationSeconds) {
                return 1d;
            }

            return elapsedSeconds / motionComponent.TravelDurationSeconds;
        }

    }
}
