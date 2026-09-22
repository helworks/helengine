namespace helengine {
    /// <summary>Regression coverage for trigger transitions and non-physical overlap handling.</summary>
    public sealed class HelPhysicsTriggerWorldTests {

        /// <summary>Verifies body handle equality and hashing retain world ownership identity for keyed collections.</summary>
        [Fact]
        public void BodyHandle_ValueEqualityIncludesWorldOwnership() {
            HelPhysicsBodyHandle3D first = new HelPhysicsBodyHandle3D(3, 7, 11);
            HelPhysicsBodyHandle3D same = new HelPhysicsBodyHandle3D(3, 7, 11);
            HelPhysicsBodyHandle3D otherWorld = new HelPhysicsBodyHandle3D(3, 7, 12);
            Dictionary<HelPhysicsBodyHandle3D, string> values = new Dictionary<HelPhysicsBodyHandle3D, string> {
                [first] = "first"
            };

            Assert.True(first.Equals(same));
            Assert.Equal(first, same);
            Assert.Equal(first.GetHashCode(), same.GetHashCode());
            Assert.NotEqual(first, otherWorld);
            Assert.False(first.Equals(otherWorld));
            Assert.False(values.ContainsKey(otherWorld));
            Assert.Equal("first", values[same]);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void TriggerPairs_PublishEnterStayExitForEveryTriggerAndDoNotApplyImpulse() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                16, 16, 32, 16, 64, 16, 32, 4, 1, 0.05, PhysicsVector3.Zero);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsBodyDescription3D triggerDescription = new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f)), BodyKind3D.Static,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.Zero, Material(), 1, ushort.MaxValue, 1, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, false, true);
            HelPhysicsBodyDescription3D movingDescription = new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.25f)), BodyKind3D.Dynamic,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, new PhysicsVector3(1f, 0f, 0f), PhysicsVector3.Zero,
                PhysicsScalar.One, Material(), 1, ushort.MaxValue, 3, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, true);
            HelPhysicsBodyHandle3D triggerA = world.CreateBody(triggerDescription);
            HelPhysicsBodyHandle3D triggerB = world.CreateBody(triggerDescription);
            HelPhysicsBodyHandle3D moving = world.CreateBody(movingDescription);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(4, world.TriggerEventCount);
            Assert.All(world.TriggerEvents, eventValue => Assert.Equal(HelPhysicsTriggerEventKind3D.Enter, eventValue.Kind));
            int populatedEnumerationCount = 0;
            foreach (HelPhysicsTriggerEvent3D eventValue in world.TriggerEvents) {
                populatedEnumerationCount++;
                Assert.Equal(HelPhysicsTriggerEventKind3D.Enter, eventValue.Kind);
            }
            Assert.Equal(4, populatedEnumerationCount);

            world.Step(settings.FixedStepSeconds);
            Assert.Equal(4, world.TriggerEventCount);
            Assert.All(world.TriggerEvents, eventValue => Assert.Equal(HelPhysicsTriggerEventKind3D.Stay, eventValue.Kind));
            Assert.Equal(0, world.LastStepMetrics.ManifoldCount);
            Assert.InRange(world.GetBodySnapshot(moving).Position.X.ToFloat(), 0.099f, 0.101f);

            world.RemoveBody(moving);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(4, world.TriggerEventCount);
            Assert.Equal(2, world.TriggerEvents.Count(eventValue => eventValue.Kind == HelPhysicsTriggerEventKind3D.Exit));
            Assert.Equal(2, world.TriggerEvents.Count(eventValue => eventValue.Kind == HelPhysicsTriggerEventKind3D.Stay));
            Assert.Contains(world.TriggerEvents, eventValue => eventValue.TriggerBody.Equals(triggerA));
            Assert.Contains(world.TriggerEvents, eventValue => eventValue.TriggerBody.Equals(triggerB));
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void TriggerFilters_SuppressPairsWhenLayersDoNotMatch() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8, 8, 16, 8, 32, 8, 16, 2, 1, 0.05, PhysicsVector3.Zero);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsMaterial3D material = Material();
            world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f)), BodyKind3D.Static,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.Zero, material, 1, 1, 1, PhysicsScalar.Zero, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, false, true));
            world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.25f)), BodyKind3D.Dynamic,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.One, material, 2, 2, 2, PhysicsScalar.Zero, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, true));
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(0, world.TriggerEventCount);
            Assert.Equal(0, world.LastStepMetrics.ManifoldCount);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void TriggerRemovalAndReuse_UsesGenerationalHandlesForExitAndEnter() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8, 8, 16, 8, 32, 8, 16, 2, 1, 0.05, PhysicsVector3.Zero);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsMaterial3D material = Material();
            HelPhysicsBodyHandle3D trigger = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f)), BodyKind3D.Static,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.Zero, material, 1, ushort.MaxValue, 1, PhysicsScalar.Zero, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, false, true));
            HelPhysicsBodyDescription3D movingDescription = new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.25f)), BodyKind3D.Dynamic,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.One, material, 1, ushort.MaxValue, 2, PhysicsScalar.Zero, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, 1, true);
            HelPhysicsBodyHandle3D oldMoving = world.CreateBody(movingDescription);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(1, world.TriggerEventCount);
            Assert.Equal(HelPhysicsTriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);

            world.RemoveBody(oldMoving);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(1, world.TriggerEventCount);
            Assert.Equal(HelPhysicsTriggerEventKind3D.Exit, world.TriggerEvents[0].Kind);
            Assert.Equal(oldMoving, world.TriggerEvents[0].OtherBody);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(0, world.TriggerEventCount);
            Assert.Empty(world.TriggerEvents);
            int emptyEnumerationCount = 0;
            foreach (HelPhysicsTriggerEvent3D eventValue in world.TriggerEvents) {
                emptyEnumerationCount++;
            }
            Assert.Equal(0, emptyEnumerationCount);

            HelPhysicsBodyHandle3D newMoving = world.CreateBody(movingDescription);
            Assert.NotEqual(oldMoving.Generation, newMoving.Generation);
            world.Step(settings.FixedStepSeconds);
            Assert.Equal(1, world.TriggerEventCount);
            Assert.Equal(HelPhysicsTriggerEventKind3D.Enter, world.TriggerEvents[0].Kind);
            Assert.Equal(newMoving, world.TriggerEvents[0].OtherBody);
            Assert.Equal(trigger, world.TriggerEvents[0].TriggerBody);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void TriggerEventView_IndexedAccessUsesValidPrefixWithoutAllocation() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8, 8, 16, 8, 32, 8, 16, 2, 1, 0.05, PhysicsVector3.Zero);
            HelPhysicsMaterial3D material = Material();
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsBodyHandle3D trigger = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f)),
                BodyKind3D.Static,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                material,
                1,
                ushort.MaxValue,
                1,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false,
                true));
            HelPhysicsBodyHandle3D moving = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.25f)),
                BodyKind3D.Dynamic,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                material,
                1,
                ushort.MaxValue,
                2,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                true));
            world.Step(settings.FixedStepSeconds);

            IReadOnlyList<HelPhysicsTriggerEvent3D> view = world.TriggerEvents;
            Assert.Same(view, world.TriggerEvents);
            Assert.Equal(1, view.Count);
            Assert.Equal(HelPhysicsTriggerEventKind3D.Enter, view[0].Kind);
            Assert.Equal(trigger, view[0].TriggerBody);
            Assert.Equal(moving, view[0].OtherBody);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1024; index++) {
                _ = view[index & 0];
            }

            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = view[1]);

            world.RemoveBody(moving);
            world.Step(settings.FixedStepSeconds);
            world.Step(settings.FixedStepSeconds);
            Assert.Empty(world.TriggerEvents);
            int emptyEnumerationCount = 0;
            foreach (HelPhysicsTriggerEvent3D eventValue in world.TriggerEvents) {
                emptyEnumerationCount++;
            }

            Assert.Equal(0, emptyEnumerationCount);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void SceneBinderMutationApis_ValidateOwnerWakeAndTeleportState() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8, 8, 16, 8, 32, 8, 16, 2, 1, 0.05, PhysicsVector3.Zero);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsSceneBinder3D owner = new HelPhysicsSceneBinder3D(world);
            HelPhysicsBodyDescription3D description = new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)), BodyKind3D.Dynamic,
                PhysicsVector3.Zero, PhysicsQuaternion.Identity, PhysicsVector3.Zero, PhysicsVector3.Zero,
                PhysicsScalar.One, Material(), 1, ushort.MaxValue, 1, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.1f), 2, false);
            HelPhysicsBodyHandle3D handle = world.CreateBodyForSceneBinder(owner, description);
            owner.Step();
            Assert.False(world.GetBodySnapshot(handle).IsAwake);

            world.SetDynamicVelocityForSceneBinder(
                owner, handle, new PhysicsVector3(1f, 0f, 0f), PhysicsVector3.Zero);
            HelPhysicsBodySnapshot3D woken = world.GetBodySnapshot(handle);
            Assert.True(woken.IsAwake);
            Assert.Equal(1f, woken.LinearVelocity.X.ToFloat());

            world.SetDynamicStateForSceneBinder(
                owner, handle, new PhysicsVector3(4f, 2f, 0f), PhysicsQuaternion.Identity,
                new PhysicsVector3(2f, 0f, 0f), PhysicsVector3.Zero);
            HelPhysicsBodySnapshot3D teleported = world.GetBodySnapshot(handle);
            Assert.Equal(4f, teleported.Position.X.ToFloat());
            Assert.Equal(2f, teleported.Position.Y.ToFloat());
            Assert.Equal(2f, teleported.LinearVelocity.X.ToFloat());
            Assert.Equal(0, teleported.LowMotionStepCount);
            world.DisposeForSceneBinder(owner);
        }

        static HelPhysicsMaterial3D Material() {
            return new HelPhysicsMaterial3D(PhysicsScalar.FromFloat(0.5f), PhysicsScalar.FromFloat(0.5f), PhysicsScalar.Zero);
        }
    }
}
