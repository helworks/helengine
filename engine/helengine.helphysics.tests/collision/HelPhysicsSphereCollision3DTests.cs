namespace helengine {
    /// <summary>Regression coverage for sphere contact orientation and stable degenerate handling.</summary>
    public sealed class HelPhysicsSphereCollision3DTests {
        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void SphereSphere_OverlapAndCoincidentCenters_AreDeterministic() {
            HelPhysicsSphereShape3D sphere = new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(1f));
            HelPhysicsBodyState3D bodyA = Body(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = Body(new PhysicsVector3(1.5f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsContactManifold3D manifold = default;
            Assert.True(HelPhysicsSphereCollision3D.TryBuildSphereSphere(in sphere, in bodyA, in sphere, in bodyB, ref manifold));
            Assert.Equal(1, manifold.ContactCount);
            AssertVector(new PhysicsVector3(1f, 0f, 0f), manifold.Contact0.Normal, 0.0001f);
            Assert.InRange(manifold.Contact0.PenetrationDepth.ToFloat(), 0.49f, 0.51f);

            bodyB = Body(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            Assert.True(HelPhysicsSphereCollision3D.TryBuildSphereSphere(in sphere, in bodyA, in sphere, in bodyB, ref manifold));
            AssertVector(PhysicsVector3.UnitX, manifold.Contact0.Normal, 0.0001f);
            Assert.InRange(manifold.Contact0.PenetrationDepth.ToFloat(), 1.99f, 2.01f);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void SphereRotatedBox_OutsideAndReversedOrder_UseOppositeNormals() {
            HelPhysicsSphereShape3D sphere = new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.5f));
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 0.5f, 0.5f));
            PhysicsQuaternion rotation = PhysicsQuaternion.CreateFromAxisAngle(PhysicsVector3.UnitZ, PhysicsScalar.FromFloat(0.5f));
            HelPhysicsBodyState3D sphereBody = Body(new PhysicsVector3(1.2f, 0.1f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D boxBody = Body(PhysicsVector3.Zero, rotation);
            HelPhysicsContactManifold3D first = default;
            HelPhysicsContactManifold3D reverse = default;
            Assert.True(HelPhysicsSphereCollision3D.TryBuildSphereBox(in sphere, in sphereBody, in box, in boxBody, ref first));
            Assert.True(HelPhysicsSphereCollision3D.TryBuildBoxSphere(in box, in boxBody, in sphere, in sphereBody, ref reverse));
            AssertVector(new PhysicsVector3(-first.Contact0.Normal.X.ToFloat(), -first.Contact0.Normal.Y.ToFloat(), -first.Contact0.Normal.Z.ToFloat()), reverse.Contact0.Normal, 0.0001f);
            Assert.Equal(first.Contact0.PenetrationDepth, reverse.Contact0.PenetrationDepth);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void SphereEmbeddedBox_ContactPointsAtNearestExitFace() {
            HelPhysicsSphereShape3D sphere = new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.25f));
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D sphereBody = Body(new PhysicsVector3(0.8f, 0.1f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D boxBody = Body(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsContactManifold3D manifold = default;
            Assert.True(HelPhysicsSphereCollision3D.TryBuildSphereBox(in sphere, in sphereBody, in box, in boxBody, ref manifold));
            AssertVector(new PhysicsVector3(-1f, 0f, 0f), manifold.Contact0.Normal, 0.0001f);
            Assert.InRange(manifold.Contact0.Position.X.ToFloat(), 0.76f, 0.79f);
            Assert.InRange(manifold.Contact0.PenetrationDepth.ToFloat(), 0.44f, 0.46f);
        }

        /// <summary>Verifies one focused HelPhysics migration invariant.</summary>
        [Fact]
        public void WorldSphere_SettlesOnStaticBoxUnderGravity() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                8, 8, 32, 16, 64, 8, 32, 4, 1, 0.05, new PhysicsVector3(0f, -9.81f, 0f));
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsMaterial3D material = new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(0.5f), PhysicsScalar.FromFloat(0.5f), PhysicsScalar.Zero);
            HelPhysicsBodyHandle3D ground = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(4f, 0.5f, 4f)), BodyKind3D.Static,
                new PhysicsVector3(0f, -0.5f, 0f), PhysicsQuaternion.Identity,
                PhysicsVector3.Zero, PhysicsVector3.Zero, PhysicsScalar.Zero, material,
                1, ushort.MaxValue, 1, PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero,
                PhysicsScalar.Zero, PhysicsScalar.Zero, 1, false));
            HelPhysicsBodyHandle3D sphere = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsSphereShape3D(PhysicsScalar.FromFloat(0.5f)), BodyKind3D.Dynamic,
                new PhysicsVector3(0f, 3f, 0f), PhysicsQuaternion.Identity,
                PhysicsVector3.Zero, PhysicsVector3.Zero, PhysicsScalar.One, material,
                1, ushort.MaxValue, 2, PhysicsScalar.One, PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.1f), PhysicsScalar.FromFloat(0.05f),
                PhysicsScalar.FromFloat(0.05f), 5, true));
            for (int step = 0; step < 80; step++) {
                world.Step(settings.FixedStepSeconds);
            }

            HelPhysicsBodySnapshot3D snapshot = world.GetBodySnapshot(sphere);
            Assert.InRange(snapshot.Position.Y.ToFloat(), 0.45f, 0.75f);
            Assert.InRange(Math.Abs(snapshot.LinearVelocity.Y.ToFloat()), 0f, 0.2f);
            world.RemoveBody(ground);
        }

        static HelPhysicsBodyState3D Body(PhysicsVector3 position, PhysicsQuaternion orientation) {
            return new HelPhysicsBodyState3D { Position = position, Orientation = orientation };
        }

        static void AssertVector(PhysicsVector3 expected, PhysicsVector3 actual, float tolerance) {
            Assert.InRange(actual.X.ToFloat(), expected.X.ToFloat() - tolerance, expected.X.ToFloat() + tolerance);
            Assert.InRange(actual.Y.ToFloat(), expected.Y.ToFloat() - tolerance, expected.Y.ToFloat() + tolerance);
            Assert.InRange(actual.Z.ToFloat(), expected.Z.ToFloat() - tolerance, expected.Z.ToFloat() + tolerance);
        }
    }
}
