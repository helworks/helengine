namespace helengine {
    /// <summary>
    /// Verifies transactional scene-binding lifecycle, preflight, synchronization, and transform validation boundaries.
    /// </summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsSceneSynchronizationHardeningTests {
        /// <summary>
        /// Initializes the minimal engine core required by real entity and component fixtures.
        /// </summary>
        public HelPhysicsSceneSynchronizationHardeningTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ensures raw world removal of an active dynamic body reconciles its binding before stepping and write-back.
        /// </summary>
        [Fact]
        public void Step_AfterRawWorldRemovalOfDynamicBinding_InvalidatesBindingWithoutWriteBackFailure() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;

            binder.World.RemoveBody(staleHandle);
            Assert.True(binding.GetBodySnapshot().IsRemovalPending);
            binder.Step();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Equal(2, entity.Components.Count);
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
        }

        /// <summary>
        /// Ensures raw world removal of an active kinematic body reconciles its binding before pre-step input synchronization.
        /// </summary>
        [Fact]
        public void Step_AfterRawWorldRemovalOfKinematicBinding_InvalidatesBindingWithoutInputFailure() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));
            binder.BindHierarchy(entity);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;

            binder.World.RemoveBody(staleHandle);
            Assert.True(binding.GetBodySnapshot().IsRemovalPending);
            binder.Step();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Equal(2, entity.Components.Count);
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
        }

        /// <summary>
        /// Ensures explicit unbind accepts an exact removal already queued through the public world without duplicate queueing.
        /// </summary>
        [Fact]
        public void Unbind_AfterRawWorldRemoval_InvalidatesBindingWithoutDuplicateRemoval() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;
            binder.World.RemoveBody(staleHandle);

            binder.Unbind(entity);

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
        }

        /// <summary>
        /// Ensures entity disposal accepts an exact removal already queued through the public world without stranding lifecycle state.
        /// </summary>
        [Fact]
        public void DisposeEntity_AfterRawWorldRemoval_InvalidatesBindingWithoutDuplicateRemoval() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;
            binder.World.RemoveBody(staleHandle);

            entity.Dispose();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
        }

        /// <summary>
        /// Ensures a hierarchy exceeding body, shape, and activation capacity fails before reserving its first body.
        /// </summary>
        [Fact]
        public void BindHierarchy_WithTwoBodiesAndSingleReservationCapacity_RejectsWithoutPartialBinding() {
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(float3.Zero);
            Entity first = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            Entity second = HelPhysicsTestSceneFactory3D.CreateBoxEntity(new float3(2f, 0f, 0f), float3.One, BodyKind3D.Dynamic);
            root.AddChild(first);
            root.AddChild(second);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));

            Assert.Throws<HelPhysicsCapacityExceededException>(() => binder.BindHierarchy(root));

            Assert.Empty(binder.Bindings);
            Assert.Equal(2, first.Components.Count);
            Assert.Equal(2, second.Components.Count);
            Entity replacement = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            binder.BindHierarchy(replacement);
            Assert.True(Assert.Single(binder.Bindings).GetBodySnapshot().IsPending);
        }

        /// <summary>
        /// Ensures pending kinematic input coalesces with activation when only one general command slot exists.
        /// </summary>
        [Fact]
        public void Step_WithPendingKinematicAndSingleCommandSlot_AppliesAuthoredState() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));
            binder.BindHierarchy(entity);
            entity.LocalPosition = new float3(2f, 3f, 4f);
            rigidBody.LinearVelocity = new float3(5f, 6f, 7f);
            rigidBody.AngularVelocity = new float3(0.1f, 0.2f, 0.3f);

            binder.Step();

            HelPhysicsBodySnapshot3D snapshot = Assert.Single(binder.Bindings).GetBodySnapshot();
            Assert.True(snapshot.IsActive);
            Assert.Equal(2f, snapshot.Position.X.ToFloat());
            Assert.Equal(3f, snapshot.Position.Y.ToFloat());
            Assert.Equal(4f, snapshot.Position.Z.ToFloat());
            Assert.Equal(5f, snapshot.LinearVelocity.X.ToFloat());
            Assert.Equal(6f, snapshot.LinearVelocity.Y.ToFloat());
            Assert.Equal(7f, snapshot.LinearVelocity.Z.ToFloat());
        }

        /// <summary>
        /// Ensures insufficient capacity for multiple active kinematics rejects before any command and permits a one-body retry.
        /// </summary>
        [Fact]
        public void Step_WithInsufficientActiveKinematicBatchCapacity_RejectsAtomicallyAndPermitsRetry() {
            Entity first = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            Entity second = HelPhysicsTestSceneFactory3D.CreateBoxEntity(new float3(10f, 0f, 0f), float3.One, BodyKind3D.Kinematic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(2, 1));
            binder.BindHierarchy(first);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            binder.BindHierarchy(second);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D firstBinding = binder.GetBinding(first);
            HelPhysicsEntityBinding3D secondBinding = binder.GetBinding(second);
            HelPhysicsBodySnapshot3D firstBefore = firstBinding.GetBodySnapshot();
            HelPhysicsBodySnapshot3D secondBefore = secondBinding.GetBodySnapshot();
            first.LocalPosition = new float3(2f, 0f, 0f);
            second.LocalPosition = new float3(12f, 0f, 0f);

            Assert.Throws<HelPhysicsCapacityExceededException>(() => binder.Step());

            AssertBodyPoseEqual(firstBefore, firstBinding.GetBodySnapshot());
            AssertBodyPoseEqual(secondBefore, secondBinding.GetBodySnapshot());
            binder.Unbind(second);
            binder.Step();
            Assert.Equal(2f, firstBinding.GetBodySnapshot().Position.X.ToFloat());
            Assert.False(secondBinding.IsValid);
        }

        /// <summary>
        /// Ensures an invalid later kinematic input rejects the whole batch without contaminating a corrected retry.
        /// </summary>
        [Fact]
        public void Step_WithInvalidSecondKinematicState_RejectsAtomicallyAndPermitsCorrectedRetry() {
            Entity first = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            Entity second = HelPhysicsTestSceneFactory3D.CreateBoxEntity(new float3(10f, 0f, 0f), float3.One, BodyKind3D.Kinematic);
            RigidBody3DComponent secondRigidBody = Assert.IsType<RigidBody3DComponent>(second.Components[0]);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(2, 2));
            binder.BindHierarchy(first);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            binder.BindHierarchy(second);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D firstBinding = binder.GetBinding(first);
            HelPhysicsEntityBinding3D secondBinding = binder.GetBinding(second);
            HelPhysicsBodySnapshot3D firstBefore = firstBinding.GetBodySnapshot();
            HelPhysicsBodySnapshot3D secondBefore = secondBinding.GetBodySnapshot();
            first.LocalPosition = new float3(3f, 0f, 0f);
            secondRigidBody.LinearVelocity = new float3(float.NaN, 0f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => binder.Step());

            AssertBodyPoseEqual(firstBefore, firstBinding.GetBodySnapshot());
            AssertBodyPoseEqual(secondBefore, secondBinding.GetBodySnapshot());
            secondRigidBody.LinearVelocity = new float3(2f, 0f, 0f);
            binder.Step();
            Assert.Equal(3f, firstBinding.GetBodySnapshot().Position.X.ToFloat());
            Assert.True(secondBinding.GetBodySnapshot().LinearVelocity.X > PhysicsScalar.Zero);
        }

        /// <summary>
        /// Ensures changing either immutable body-mode direction fails at the binding boundary before simulation mutation.
        /// </summary>
        /// <param name="originalKind">Body mode captured by the binding description.</param>
        /// <param name="replacementKind">Different live component mode that must be rejected.</param>
        [Theory]
        [InlineData(BodyKind3D.Dynamic, BodyKind3D.Kinematic)]
        [InlineData(BodyKind3D.Kinematic, BodyKind3D.Dynamic)]
        public void Step_AfterBoundBodyModeChanges_RejectsBeforeWorldMutation(
            BodyKind3D originalKind,
            BodyKind3D replacementKind) {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, originalKind);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 2));
            binder.BindHierarchy(entity);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodySnapshot3D before = binding.GetBodySnapshot();
            rigidBody.BodyKind = replacementKind;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => binder.Step());

            Assert.Contains("body mode", exception.Message, StringComparison.OrdinalIgnoreCase);
            AssertBodyPoseEqual(before, binding.GetBodySnapshot());
            Assert.True(binding.IsValid);
            binder.Unbind(entity);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            Assert.False(binding.IsValid);
        }

        /// <summary>
        /// Ensures replacing either original bound component fails identity validation while leaving explicit unbind recoverable.
        /// </summary>
        /// <param name="componentKind">Original component role to remove and replace.</param>
        [Theory]
        [InlineData("rigid-body-removed")]
        [InlineData("rigid-body-replaced")]
        [InlineData("collider-removed")]
        [InlineData("collider-replaced")]
        public void Step_AfterOriginalBoundComponentIsRemovedOrReplaced_RejectsBeforeWorldMutation(string componentKind) {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            RigidBody3DComponent originalRigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            BoxCollider3DComponent originalCollider = Assert.IsType<BoxCollider3DComponent>(entity.Components[1]);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 2));
            binder.BindHierarchy(entity);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodySnapshot3D before = binding.GetBodySnapshot();
            if (componentKind == "rigid-body-removed") {
                entity.RemoveComponent(originalRigidBody);
            } else if (componentKind == "rigid-body-replaced") {
                entity.RemoveComponent(originalRigidBody);
                entity.AddComponent(new RigidBody3DComponent());
            } else if (componentKind == "collider-removed") {
                entity.RemoveComponent(originalCollider);
            } else {
                entity.RemoveComponent(originalCollider);
                entity.AddComponent(new BoxCollider3DComponent());
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => binder.Step());

            Assert.Contains("original", exception.Message, StringComparison.OrdinalIgnoreCase);
            AssertBodyPoseEqual(before, binding.GetBodySnapshot());
            Assert.True(binding.IsValid);
            binder.Unbind(entity);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            Assert.False(binding.IsValid);
        }

        /// <summary>
        /// Ensures zero and non-finite parent scale reject before advancing a moving dynamic body or publishing local pose.
        /// </summary>
        /// <param name="invalidScaleX">Invalid effective parent X scale.</param>
        [Theory]
        [InlineData(0f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Step_WithInvalidDynamicParentScale_RejectsBeforeSimulationAndEntityMutation(float invalidScaleX) {
            Entity parent = HelPhysicsTestSceneFactory3D.CreateEntity(float3.Zero);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            rigidBody.LinearVelocity = float3.UnitX;
            parent.AddChild(entity);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 2));
            binder.BindHierarchy(parent);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodySnapshot3D before = binding.GetBodySnapshot();
            float3 localPositionBefore = entity.LocalPosition;
            float4 localOrientationBefore = entity.LocalOrientation;
            parent.LocalScale = new float3(invalidScaleX, 1f, 1f);

            Assert.Throws<InvalidOperationException>(() => binder.Step());

            AssertBodyPoseEqual(before, binding.GetBodySnapshot());
            Assert.Equal(localPositionBefore, entity.LocalPosition);
            Assert.Equal(localOrientationBefore, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures a non-invertible parent orientation rejects before advancing a moving dynamic body or publishing local pose.
        /// </summary>
        [Fact]
        public void Step_WithNonInvertibleDynamicParentOrientation_RejectsBeforeSimulationAndEntityMutation() {
            Entity parent = HelPhysicsTestSceneFactory3D.CreateEntity(float3.Zero);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            rigidBody.LinearVelocity = float3.UnitX;
            parent.AddChild(entity);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 2));
            binder.BindHierarchy(parent);
            binder.World.Step(binder.World.Settings.FixedStepSeconds);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodySnapshot3D before = binding.GetBodySnapshot();
            float3 localPositionBefore = entity.LocalPosition;
            float4 localOrientationBefore = entity.LocalOrientation;
            parent.LocalOrientation = float4.Zero;

            Assert.Throws<InvalidOperationException>(() => binder.Step());

            AssertBodyPoseEqual(before, binding.GetBodySnapshot());
            Assert.Equal(localPositionBefore, entity.LocalPosition);
            Assert.Equal(localOrientationBefore, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures unsupported non-static-mesh collider diagnostics remain explicit without runtime type-name discovery.
        /// </summary>
        [Fact]
        public void BindHierarchy_WithUnsupportedCapsuleCollider_UsesGenericReflectionFreeDiagnostic() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateEntity(float3.Zero);
            entity.AddComponent(new RigidBody3DComponent());
            entity.AddComponent(new CapsuleCollider3DComponent());
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 1));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => binder.BindHierarchy(entity));

            Assert.Equal(
                "HelPhysics scene binding does not support this Collider3DComponent implementation.",
                exception.Message);
            Assert.Empty(binder.Bindings);
        }

        /// <summary>
        /// Creates an explicit zero-gravity world profile with matched body and shape capacity.
        /// </summary>
        /// <param name="bodyCapacity">Body, shape, island, and broadphase working capacity.</param>
        /// <param name="commandCapacity">General deferred-command capacity.</param>
        /// <returns>A valid fixed world profile for synchronization boundary tests.</returns>
        static HelPhysicsWorldSettings3D CreateSettings(int bodyCapacity, int commandCapacity) {
            int manifoldCapacity = bodyCapacity == 1 ? 1 : 2;
            return new HelPhysicsWorldSettings3D(
                bodyCapacity,
                bodyCapacity,
                bodyCapacity,
                manifoldCapacity,
                bodyCapacity * 4,
                bodyCapacity,
                commandCapacity,
                1,
                1,
                0.05d,
                PhysicsVector3.Zero);
        }

        /// <summary>
        /// Asserts exact simulation pose equality while deliberately ignoring velocity and lifecycle flags.
        /// </summary>
        /// <param name="expected">Snapshot captured before a rejected transaction.</param>
        /// <param name="actual">Snapshot observed after the rejected transaction.</param>
        static void AssertBodyPoseEqual(HelPhysicsBodySnapshot3D expected, HelPhysicsBodySnapshot3D actual) {
            Assert.Equal(expected.Position, actual.Position);
            Assert.Equal(expected.Orientation, actual.Orientation);
        }
    }
}
