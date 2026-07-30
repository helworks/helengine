namespace helengine {
    /// <summary>
    /// Verifies that the public HelPhysics scene runtime binds and synchronizes supported engine-authored box entities.
    /// </summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsSceneBindingTests {
        /// <summary>
        /// Initializes the minimal engine core required by real entity fixtures.
        /// </summary>
        public HelPhysicsSceneBindingTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ensures recursive traversal binds each supported nested physics entity exactly once.
        /// </summary>
        [Fact]
        public void BindHierarchy_WithNestedGroundAndFourBoxes_BindsExactlyFiveBodies() {
            Entity root = HelPhysicsTestSceneFactory3D.CreateNestedGroundAndFourBoxScene();
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            binder.BindHierarchy(root);

            Assert.Equal(5, binder.Bindings.Count);
            Assert.Equal(1, binder.Bindings.Count(binding => binding.GetBodySnapshot().BodyKind == BodyKind3D.Static));
            Assert.Equal(4, binder.Bindings.Count(binding => binding.GetBodySnapshot().BodyKind == BodyKind3D.Dynamic));
            Assert.Equal(5, binder.Bindings.Select(binding => binding.Entity).Distinct().Count());
            HelPhysicsBodyDescription3D staticDescription = binder.Bindings
                .Single(binding => binding.Description.BodyKind == BodyKind3D.Static)
                .Description;
            Assert.Equal(PhysicsScalar.Zero, staticDescription.Mass);
            Assert.Equal(PhysicsScalar.Zero, staticDescription.InverseMass);
            Assert.Equal(PhysicsScalar.Zero, staticDescription.LocalInverseInertia.Row0.X);
            Assert.Equal(PhysicsScalar.Zero, staticDescription.LocalInverseInertia.Row1.Y);
            Assert.Equal(PhysicsScalar.Zero, staticDescription.LocalInverseInertia.Row2.Z);
            Assert.False(staticDescription.IsAwake);
        }

        /// <summary>
        /// Ensures strict translation rejects missing, ambiguous, or unsupported physics component compositions.
        /// </summary>
        /// <param name="invalidCase">Named malformed component composition.</param>
        /// <param name="expectedTypeName">Exact component class that the diagnostic must identify.</param>
        [Theory]
        [InlineData("collider-without-body", "BoxCollider3DComponent")]
        [InlineData("body-without-collider", "RigidBody3DComponent")]
        [InlineData("multiple-colliders", "BoxCollider3DComponent")]
        [InlineData("multiple-rigid-bodies", "RigidBody3DComponent")]
        [InlineData("static-mesh", "StaticMeshCollider3DComponent")]
        public void BindHierarchy_WithInvalidComponentComposition_RejectsEntity(
            string invalidCase,
            string expectedTypeName) {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateInvalidPhysicsEntity(invalidCase);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => binder.BindHierarchy(entity));

            Assert.Contains(expectedTypeName, exception.Message, StringComparison.Ordinal);
            Assert.Empty(binder.Bindings);
        }

        /// <summary>
        /// Ensures non-finite, zero, and negative effective box scales reject the complete hierarchy before any reservation.
        /// </summary>
        /// <param name="invalidX">Invalid effective X scale supplied to the second box.</param>
        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void BindHierarchy_WithInvalidEffectiveBoxScale_RejectsWithoutPartialBinding(float invalidX) {
            Entity root = HelPhysicsTestSceneFactory3D.CreateHierarchyWithInvalidScaledBox(new float3(invalidX, 1f, 1f));
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            Assert.Throws<ArgumentOutOfRangeException>(() => binder.BindHierarchy(root));

            Assert.Empty(binder.Bindings);
        }

        /// <summary>
        /// Ensures supported authoring values are translated explicitly into one complete HelPhysics body description.
        /// </summary>
        [Fact]
        public void BindHierarchy_WithAuthoredScaledDynamicBox_TranslatesCompleteDescription() {
            Entity parent = HelPhysicsTestSceneFactory3D.CreateEntity(new float3(10f, 20f, 30f));
            parent.LocalScale = new float3(2f, 3f, 4f);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(1f, 2f, 3f),
                new float3(2f, 4f, 0.5f),
                BodyKind3D.Dynamic);
            entity.LocalScale = new float3(1.5f, 0.5f, 2f);
            float4.CreateFromYawPitchRoll(0.25f, -0.1f, 0.4f, out float4 authoredOrientation);
            entity.LocalOrientation = authoredOrientation;
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            rigidBody.LinearVelocity = new float3(1f, 2f, 3f);
            rigidBody.AngularVelocity = new float3(4f, 5f, 6f);
            rigidBody.Mass = 7d;
            rigidBody.GravityScale = 0.25d;
            rigidBody.SleepThreshold = 0.2d;
            rigidBody.SleepTicks = 6;
            BoxCollider3DComponent collider = Assert.IsType<BoxCollider3DComponent>(entity.Components[1]);
            collider.CollisionLayer = 4;
            collider.CollisionMask = 7;
            collider.StaticFriction = 0.9d;
            collider.DynamicFriction = 0.3d;
            collider.Restitution = 0.2d;
            parent.AddChild(entity);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            binder.BindHierarchy(parent);

            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyDescription3D description = binding.Description;
            Assert.Equal(3f, description.Shape.HalfExtents.X.ToFloat());
            Assert.Equal(3f, description.Shape.HalfExtents.Y.ToFloat());
            Assert.Equal(2f, description.Shape.HalfExtents.Z.ToFloat());
            Assert.Equal(12f, description.Position.X.ToFloat());
            Assert.Equal(26f, description.Position.Y.ToFloat());
            Assert.Equal(42f, description.Position.Z.ToFloat());
            Assert.Equal(authoredOrientation.X, description.Orientation.X.ToFloat(), 5);
            Assert.Equal(authoredOrientation.Y, description.Orientation.Y.ToFloat(), 5);
            Assert.Equal(authoredOrientation.Z, description.Orientation.Z.ToFloat(), 5);
            Assert.Equal(authoredOrientation.W, description.Orientation.W.ToFloat(), 5);
            Assert.Equal(1f, description.LinearVelocity.X.ToFloat());
            Assert.Equal(5f, description.AngularVelocity.Y.ToFloat());
            Assert.Equal(7f, description.Mass.ToFloat());
            Assert.True(description.LocalInverseInertia.Row0.X > PhysicsScalar.Zero);
            Assert.Equal(0.25f, description.GravityScale.ToFloat());
            Assert.Equal(4, description.CollisionLayer);
            Assert.Equal(7, description.CollisionMask);
            Assert.Equal(0.9f, description.Material.StaticFriction.ToFloat());
            Assert.Equal(0.3f, description.Material.DynamicFriction.ToFloat());
            Assert.Equal(0.2f, description.Material.Restitution.ToFloat());
            Assert.Equal(0.1f, description.LinearDamping.ToFloat());
            Assert.Equal(0.1f, description.AngularDamping.ToFloat());
            Assert.Equal(0.2f, description.LinearSleepThreshold.ToFloat());
            Assert.Equal(0.2f, description.AngularSleepThreshold.ToFloat());
            Assert.Equal((ushort)6, description.SleepTicks);
            Assert.True(description.IsAwake);
            Assert.Equal(binding.BindingId, description.EntityBindingId);
        }

        /// <summary>
        /// Ensures one standalone step copies edited kinematic pose and authored velocity into HelPhysics before simulation.
        /// </summary>
        [Fact]
        public void Step_WithEditedKinematicEntity_SynchronizesPoseAndVelocityBeforeWorldStep() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8,
                8,
                16,
                8,
                32,
                8,
                16,
                2,
                1,
                0.05d,
                PhysicsVector3.Zero);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            binder.BindHierarchy(entity);
            entity.LocalPosition = new float3(3f, 4f, 5f);
            float4.CreateFromYawPitchRoll(0.3f, 0.2f, -0.1f, out float4 editedOrientation);
            entity.LocalOrientation = editedOrientation;
            rigidBody.LinearVelocity = new float3(6f, 7f, 8f);
            rigidBody.AngularVelocity = new float3(0.4f, 0.5f, 0.6f);

            binder.Step();

            HelPhysicsBodySnapshot3D snapshot = Assert.Single(binder.Bindings).GetBodySnapshot();
            HelPhysicsBodyDescription3D description = Assert.Single(binder.Bindings).Description;
            Assert.True(snapshot.IsActive);
            Assert.Equal(PhysicsScalar.Zero, description.Mass);
            Assert.Equal(PhysicsScalar.Zero, description.InverseMass);
            Assert.Equal(PhysicsScalar.Zero, description.LocalInverseInertia.Row0.X);
            Assert.Equal(PhysicsScalar.Zero, description.LocalInverseInertia.Row1.Y);
            Assert.Equal(PhysicsScalar.Zero, description.LocalInverseInertia.Row2.Z);
            Assert.Equal(3f, snapshot.Position.X.ToFloat());
            Assert.Equal(4f, snapshot.Position.Y.ToFloat());
            Assert.Equal(5f, snapshot.Position.Z.ToFloat());
            Assert.Equal(editedOrientation.X, snapshot.Orientation.X.ToFloat(), 5);
            Assert.Equal(editedOrientation.Y, snapshot.Orientation.Y.ToFloat(), 5);
            Assert.Equal(editedOrientation.Z, snapshot.Orientation.Z.ToFloat(), 5);
            Assert.Equal(editedOrientation.W, snapshot.Orientation.W.ToFloat(), 5);
            Assert.Equal(6f, snapshot.LinearVelocity.X.ToFloat());
            Assert.Equal(7f, snapshot.LinearVelocity.Y.ToFloat());
            Assert.Equal(8f, snapshot.LinearVelocity.Z.ToFloat());
            Assert.Equal(0.4f, snapshot.AngularVelocity.X.ToFloat());
            Assert.Equal(0.5f, snapshot.AngularVelocity.Y.ToFloat());
            Assert.Equal(0.6f, snapshot.AngularVelocity.Z.ToFloat());
        }

        /// <summary>
        /// Ensures one standalone step copies evolved dynamic pose and velocity back to an unparented entity.
        /// </summary>
        [Fact]
        public void Step_WithMovingDynamicBody_WritesPoseAndVelocityBackToEntity() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            rigidBody.LinearVelocity = new float3(2f, 1f, -3f);
            rigidBody.AngularVelocity = new float3(0f, 0.5f, 0f);
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8,
                8,
                16,
                8,
                32,
                8,
                16,
                2,
                1,
                0.05d,
                PhysicsVector3.Zero);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            binder.BindHierarchy(entity);

            binder.Step();

            HelPhysicsBodySnapshot3D snapshot = Assert.Single(binder.Bindings).GetBodySnapshot();
            Assert.Equal(snapshot.Position.X.ToFloat(), entity.LocalPosition.X);
            Assert.Equal(snapshot.Position.Y.ToFloat(), entity.LocalPosition.Y);
            Assert.Equal(snapshot.Position.Z.ToFloat(), entity.LocalPosition.Z);
            Assert.Equal(snapshot.Orientation.X.ToFloat(), entity.LocalOrientation.X, 5);
            Assert.Equal(snapshot.Orientation.Y.ToFloat(), entity.LocalOrientation.Y, 5);
            Assert.Equal(snapshot.Orientation.Z.ToFloat(), entity.LocalOrientation.Z, 5);
            Assert.Equal(snapshot.Orientation.W.ToFloat(), entity.LocalOrientation.W, 5);
            Assert.Equal(snapshot.LinearVelocity.X.ToFloat(), rigidBody.LinearVelocity.X);
            Assert.Equal(snapshot.LinearVelocity.Y.ToFloat(), rigidBody.LinearVelocity.Y);
            Assert.Equal(snapshot.LinearVelocity.Z.ToFloat(), rigidBody.LinearVelocity.Z);
            Assert.Equal(snapshot.AngularVelocity.Y.ToFloat(), rigidBody.AngularVelocity.Y);
            Assert.NotEqual(float3.Zero, entity.LocalPosition);
            Assert.NotEqual(float4.Identity, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures dynamic world-pose output is converted back into local space under a transformed parent.
        /// </summary>
        [Fact]
        public void Step_WithParentedDynamicBody_PreservesLocalTransformSemantics() {
            Entity parent = HelPhysicsTestSceneFactory3D.CreateEntity(new float3(10f, -2f, 7f));
            parent.LocalScale = new float3(2f, 3f, 4f);
            float4.CreateFromYawPitchRoll(0.7f, -0.2f, 0.1f, out float4 parentOrientation);
            parent.LocalOrientation = parentOrientation;
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(1f, 2f, -1f),
                float3.One,
                BodyKind3D.Dynamic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            rigidBody.LinearVelocity = new float3(2f, -1f, 3f);
            rigidBody.AngularVelocity = new float3(0.2f, 0.4f, -0.1f);
            parent.AddChild(entity);
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8,
                8,
                16,
                8,
                32,
                8,
                16,
                2,
                1,
                0.05d,
                PhysicsVector3.Zero);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            binder.BindHierarchy(parent);

            binder.Step();

            HelPhysicsBodySnapshot3D snapshot = Assert.Single(binder.Bindings).GetBodySnapshot();
            Assert.Equal(snapshot.Position.X.ToFloat(), entity.Position.X, 5);
            Assert.Equal(snapshot.Position.Y.ToFloat(), entity.Position.Y, 5);
            Assert.Equal(snapshot.Position.Z.ToFloat(), entity.Position.Z, 5);
            Assert.Equal(snapshot.Orientation.X.ToFloat(), entity.Orientation.X, 5);
            Assert.Equal(snapshot.Orientation.Y.ToFloat(), entity.Orientation.Y, 5);
            Assert.Equal(snapshot.Orientation.Z.ToFloat(), entity.Orientation.Z, 5);
            Assert.Equal(snapshot.Orientation.W.ToFloat(), entity.Orientation.W, 5);
            Assert.NotEqual(entity.Position, entity.LocalPosition);
            Assert.NotEqual(entity.Orientation, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures explicit unbinding invalidates the association and stale generation before a recycled slot can be addressed.
        /// </summary>
        [Fact]
        public void Unbind_WithRecycledBodySlot_InvalidatesBindingAndStaleHandle() {
            Entity firstEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            binder.BindHierarchy(firstEntity);
            binder.Step();
            HelPhysicsEntityBinding3D staleBinding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = staleBinding.BodyHandle;

            binder.Unbind(firstEntity);

            Assert.False(staleBinding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Throws<InvalidOperationException>(() => staleBinding.GetBodySnapshot());
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));

            Entity replacementEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            binder.BindHierarchy(replacementEntity);
            HelPhysicsEntityBinding3D replacementBinding = Assert.Single(binder.Bindings);
            Assert.Equal(staleHandle.Index, replacementBinding.BodyHandle.Index);
            Assert.NotEqual(staleHandle.Generation, replacementBinding.BodyHandle.Generation);
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
            Assert.True(replacementBinding.GetBodySnapshot().IsPending);
        }

        /// <summary>
        /// Ensures disposing a bound entity immediately invalidates its binding and defers removal of the owned body.
        /// </summary>
        [Fact]
        public void DisposeEntity_WhenBound_InvalidatesBindingAndDefersBodyRemoval() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D handle = binding.BodyHandle;

            entity.Dispose();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Throws<InvalidOperationException>(() => binding.GetBodySnapshot());
            Assert.True(binder.World.GetBodySnapshot(handle).IsActive);
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(handle));
        }

        /// <summary>
        /// Ensures entity disposal can replace a pending activation even when the deferred command buffer has no spare slot.
        /// </summary>
        [Fact]
        public void DisposeEntity_BeforeFirstStepWithSingleCommandSlot_InvalidatesAndRecyclesBody() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSingleBodySingleCommandSettings());
            binder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;

            entity.Dispose();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Throws<InvalidOperationException>(() => binding.GetBodySnapshot());
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));

            Entity replacementEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            binder.BindHierarchy(replacementEntity);
            HelPhysicsEntityBinding3D replacementBinding = Assert.Single(binder.Bindings);
            Assert.Equal(staleHandle.Index, replacementBinding.BodyHandle.Index);
            Assert.NotEqual(staleHandle.Generation, replacementBinding.BodyHandle.Generation);
            binder.Step();
            Assert.True(replacementBinding.GetBodySnapshot().IsActive);
        }

        /// <summary>
        /// Ensures active entity disposal remains representable when the only general command slot holds input for the same body.
        /// </summary>
        [Fact]
        public void DisposeEntity_WithActiveBodyAndFullSameBodyCommandBuffer_InvalidatesAndRemovesBody() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSingleBodySingleCommandSettings());
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;
            binder.World.ApplyImpulse(staleHandle, PhysicsVector3.UnitX);

            entity.Dispose();

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Throws<InvalidOperationException>(() => binding.GetBodySnapshot());
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));

            Entity replacementEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            binder.BindHierarchy(replacementEntity);
            HelPhysicsEntityBinding3D replacementBinding = Assert.Single(binder.Bindings);
            Assert.Equal(staleHandle.Index, replacementBinding.BodyHandle.Index);
            Assert.NotEqual(staleHandle.Generation, replacementBinding.BodyHandle.Generation);
            binder.Step();
            Assert.Equal(PhysicsVector3.Zero, replacementBinding.GetBodySnapshot().LinearVelocity);
        }

        /// <summary>
        /// Ensures active removal preserves a full-buffer input for another exact body generation while removing only the disposed entity.
        /// </summary>
        [Fact]
        public void DisposeEntity_WithFullUnrelatedBodyCommandBuffer_PreservesUnrelatedInputAndGeneration() {
            Entity firstEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            Entity secondEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(new float3(10f, 0f, 0f), float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateTwoBodySingleCommandSettings());
            binder.BindHierarchy(firstEntity);
            binder.Step();
            binder.BindHierarchy(secondEntity);
            binder.Step();
            HelPhysicsEntityBinding3D firstBinding = binder.GetBinding(firstEntity);
            HelPhysicsEntityBinding3D secondBinding = binder.GetBinding(secondEntity);
            HelPhysicsBodyHandle3D staleHandle = firstBinding.BodyHandle;
            HelPhysicsBodyHandle3D secondHandle = secondBinding.BodyHandle;
            binder.World.ApplyImpulse(secondHandle, PhysicsVector3.UnitX);

            firstEntity.Dispose();

            Assert.False(firstBinding.IsValid);
            Assert.Same(secondBinding, Assert.Single(binder.Bindings));
            Assert.True(secondBinding.IsValid);
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
            Assert.Equal(secondHandle.Index, secondBinding.BodyHandle.Index);
            Assert.Equal(secondHandle.Generation, secondBinding.BodyHandle.Generation);
            Assert.True(secondBinding.GetBodySnapshot().LinearVelocity.X > PhysicsScalar.Zero);
        }

        /// <summary>
        /// Ensures explicit unbinding queues exactly one pending removal without requiring a second command slot or lifecycle callback removal.
        /// </summary>
        [Fact]
        public void Unbind_BeforeFirstStepWithSingleCommandSlot_InvalidatesAndRemovesBodyOnce() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSingleBodySingleCommandSettings());
            binder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D staleHandle = binding.BodyHandle;

            binder.Unbind(entity);

            Assert.False(binding.IsValid);
            Assert.Empty(binder.Bindings);
            Assert.Throws<InvalidOperationException>(() => binder.Unbind(entity));
            binder.Step();
            Assert.Throws<InvalidOperationException>(() => binder.World.GetBodySnapshot(staleHandle));
        }

        /// <summary>
        /// Ensures rebinding an already owned entity cannot create a duplicate body or association.
        /// </summary>
        [Fact]
        public void BindHierarchy_WithAlreadyBoundEntity_RejectsDuplicateReservation() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            binder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D originalBinding = Assert.Single(binder.Bindings);

            Assert.Throws<InvalidOperationException>(() => binder.BindHierarchy(entity));

            Assert.Same(originalBinding, Assert.Single(binder.Bindings));
            Assert.True(originalBinding.GetBodySnapshot().IsPending);
        }

        /// <summary>
        /// Ensures a world rejects a second scene-binder owner before any entity or body reservation can occur.
        /// </summary>
        [Fact]
        public void BindHierarchy_FromSecondBinderSharingWorld_RejectsOwnedEntityBeforeReservation() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateTwoBodyWorldSettings());
            HelPhysicsSceneBinder3D firstBinder = new HelPhysicsSceneBinder3D(world);
            firstBinder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D firstBinding = Assert.Single(firstBinder.Bindings);

            Assert.Throws<InvalidOperationException>(() => new HelPhysicsSceneBinder3D(world));

            Assert.Same(firstBinding, Assert.Single(firstBinder.Bindings));
            Assert.True(firstBinding.IsValid);
            Entity secondEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            firstBinder.BindHierarchy(secondEntity);
            Assert.Equal(2, firstBinder.Bindings.Count);
        }

        /// <summary>
        /// Ensures another binder with an independent world rejects entity ownership before reserving its sole body slot.
        /// </summary>
        [Fact]
        public void BindHierarchy_FromSecondBinderWithSeparateWorld_RejectsOwnedEntityBeforeReservation() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            HelPhysicsSceneBinder3D firstBinder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            HelPhysicsSceneBinder3D secondBinder = HelPhysicsRuntimeFactory3D.Create(CreateSingleBodySingleCommandSettings());
            firstBinder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D firstBinding = Assert.Single(firstBinder.Bindings);

            Assert.Throws<InvalidOperationException>(() => secondBinder.BindHierarchy(entity));

            Assert.Same(firstBinding, Assert.Single(firstBinder.Bindings));
            Assert.True(firstBinding.IsValid);
            Assert.Empty(secondBinder.Bindings);
            Entity secondEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            secondBinder.BindHierarchy(secondEntity);
            Assert.Single(secondBinder.Bindings);
        }

        /// <summary>
        /// Ensures a future coordinator can enumerate and query bindings by exact entity identity through public APIs.
        /// </summary>
        [Fact]
        public void BindingQueries_WithBoundAndUnboundEntities_ReturnCurrentAssociationOnly() {
            Entity boundEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            Entity unboundEntity = HelPhysicsTestSceneFactory3D.CreateEntity(float3.Zero);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            binder.BindHierarchy(boundEntity);
            HelPhysicsEntityBinding3D expected = Assert.Single(binder.Bindings);

            Assert.True(binder.TryGetBinding(boundEntity, out HelPhysicsEntityBinding3D queried));
            Assert.Same(expected, queried);
            Assert.Same(expected, binder.GetBinding(boundEntity));
            Assert.False(binder.TryGetBinding(unboundEntity, out HelPhysicsEntityBinding3D missing));
            Assert.Null(missing);
            Assert.Throws<InvalidOperationException>(() => binder.GetBinding(unboundEntity));
        }

        /// <summary>
        /// Ensures standalone runtime construction rejects absent required settings and ownership references.
        /// </summary>
        [Fact]
        public void RuntimeConstruction_WithNullRequiredInputs_Throws() {
            Assert.Throws<ArgumentNullException>(() => HelPhysicsRuntimeFactory3D.Create(null));
            Assert.Throws<ArgumentNullException>(() => new HelPhysicsSceneBinder3D(null));
            Assert.Throws<ArgumentNullException>(() => new HelPhysicsEntitySynchronizer3D(null));
        }

        /// <summary>
        /// Ensures kinematic state replacement is one validated deferred command that cannot partially mutate after rejection.
        /// </summary>
        [Fact]
        public void SetKinematicState_WithRejectedSecondCommand_PreservesAcceptedCommandAtomicity() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Kinematic);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            PhysicsVector3 acceptedPosition = new PhysicsVector3(2f, 3f, 4f);
            PhysicsVector3 acceptedLinearVelocity = new PhysicsVector3(5f, 6f, 7f);
            binder.World.SetKinematicState(
                binding.BodyHandle,
                acceptedPosition,
                PhysicsQuaternion.Identity,
                acceptedLinearVelocity,
                new PhysicsVector3(0.1f, 0.2f, 0.3f));

            Assert.Equal(0f, binding.GetBodySnapshot().Position.X.ToFloat());
            Assert.Throws<ArgumentOutOfRangeException>(() => binder.World.SetKinematicState(
                binding.BodyHandle,
                new PhysicsVector3(20f, 30f, 40f),
                new PhysicsQuaternion(
                    PhysicsScalar.Zero,
                    PhysicsScalar.Zero,
                    PhysicsScalar.Zero,
                    PhysicsScalar.FromFloat(2f)),
                PhysicsVector3.Zero,
                PhysicsVector3.Zero));

            binder.World.StepForSceneBinder(binder, binder.World.Settings.FixedStepSeconds);

            HelPhysicsBodySnapshot3D snapshot = binding.GetBodySnapshot();
            Assert.Equal(2f, snapshot.Position.X.ToFloat());
            Assert.Equal(3f, snapshot.Position.Y.ToFloat());
            Assert.Equal(4f, snapshot.Position.Z.ToFloat());
            Assert.Equal(5f, snapshot.LinearVelocity.X.ToFloat());
            Assert.Equal(6f, snapshot.LinearVelocity.Y.ToFloat());
            Assert.Equal(7f, snapshot.LinearVelocity.Z.ToFloat());
        }

        /// <summary>
        /// Ensures dynamic velocity written back after one step is not blindly resent over a later world-side impulse.
        /// </summary>
        [Fact]
        public void Step_AfterDynamicVelocityWriteBack_PreservesQueuedWorldImpulse() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Dynamic);
            RigidBody3DComponent rigidBody = Assert.IsType<RigidBody3DComponent>(entity.Components[0]);
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8,
                8,
                16,
                8,
                32,
                8,
                16,
                2,
                1,
                0.05d,
                PhysicsVector3.Zero);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            binder.BindHierarchy(entity);
            binder.Step();
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            Assert.Equal(float3.Zero, rigidBody.LinearVelocity);
            binder.World.ApplyImpulse(binding.BodyHandle, new PhysicsVector3(4f, 0f, 0f));

            binder.Step();

            HelPhysicsBodySnapshot3D snapshot = binding.GetBodySnapshot();
            Assert.True(snapshot.LinearVelocity.X.ToFloat() > 3.9f);
            Assert.Equal(snapshot.LinearVelocity.X.ToFloat(), rigidBody.LinearVelocity.X);
            Assert.Equal(snapshot.LinearVelocity.Y.ToFloat(), rigidBody.LinearVelocity.Y);
            Assert.Equal(snapshot.LinearVelocity.Z.ToFloat(), rigidBody.LinearVelocity.Z);
        }

        /// <summary>
        /// Creates the smallest world profile that exposes pending activation and removal competition for one command slot.
        /// </summary>
        /// <returns>A valid one-body world profile with exactly one deferred command slot.</returns>
        static HelPhysicsWorldSettings3D CreateSingleBodySingleCommandSettings() {
            return new HelPhysicsWorldSettings3D(
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                1,
                0.05d,
                PhysicsVector3.Zero);
        }

        /// <summary>
        /// Creates a shared-world profile with exactly two body and shape reservations available to ownership tests.
        /// </summary>
        /// <returns>A valid two-body world profile with room for both accepted activation commands.</returns>
        static HelPhysicsWorldSettings3D CreateTwoBodyWorldSettings() {
            return new HelPhysicsWorldSettings3D(
                2,
                2,
                2,
                2,
                4,
                2,
                2,
                1,
                1,
                0.05d,
                PhysicsVector3.Zero);
        }

        /// <summary>
        /// Creates a two-body profile whose sole general command slot exposes unrelated input and removal ordering.
        /// </summary>
        /// <returns>A valid two-body world profile with one deferred general command slot.</returns>
        static HelPhysicsWorldSettings3D CreateTwoBodySingleCommandSettings() {
            return new HelPhysicsWorldSettings3D(
                2,
                2,
                2,
                2,
                4,
                2,
                1,
                1,
                1,
                0.05d,
                PhysicsVector3.Zero);
        }
    }
}
