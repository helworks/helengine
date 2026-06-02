namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies persistent box-box constraints preserve support impulses across BEPU-style manifold updates.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class BoxBoxContactConstraint3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed constraint tests.
        /// </summary>
        public BoxBoxContactConstraint3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Detaches the active core singleton after each test so later tests start from a clean runtime.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures aligned manifold updates redistribute cached support instead of zeroing all impulses.
        /// </summary>
        [Fact]
        public void MatchManifold_WithAlignedNormalAndChangedFeatureIds_RedistributesCachedNormalImpulses() {
            Entity firstEntity = new Entity();
            Entity secondEntity = new Entity();
            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(firstEntity, secondEntity) {
                NormalImpulse0 = 4f,
                NormalImpulse1 = 2f,
                TangentImpulse = new float3(0.5f, 0f, 0.25f),
                TwistImpulse = 0.4f,
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11,
                LastNormal = new float3(0f, 1f, 0f),
                HasLastNormal = true
            };
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = new float3(0f, 1f, 0f),
                ContactCount = 4,
                FeatureId0 = 20,
                FeatureId1 = 21,
                FeatureId2 = 22,
                FeatureId3 = 23
            };

            constraint.MatchManifold(manifold);

            Assert.Equal(1.5f, constraint.NormalImpulse0);
            Assert.Equal(1.5f, constraint.NormalImpulse1);
            Assert.Equal(1.5f, constraint.NormalImpulse2);
            Assert.Equal(1.5f, constraint.NormalImpulse3);
            Assert.Equal(new float3(0.5f, 0f, 0.25f), constraint.TangentImpulse);
            Assert.Equal(0.4f, constraint.TwistImpulse);
        }

        /// <summary>
        /// Ensures aligned manifold updates preserve exact feature matches before redistributing only the unmatched leftover support.
        /// </summary>
        [Fact]
        public void MatchManifold_WithAlignedNormalAndPartialFeatureMatches_PreservesMatchedSupportBeforeRedistribution() {
            Entity firstEntity = new Entity();
            Entity secondEntity = new Entity();
            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(firstEntity, secondEntity) {
                NormalImpulse0 = 4f,
                NormalImpulse1 = 2f,
                NormalImpulse2 = 1f,
                ContactCount = 3,
                FeatureId0 = 10,
                FeatureId1 = 11,
                FeatureId2 = 12,
                LastNormal = new float3(0f, 1f, 0f),
                HasLastNormal = true
            };
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = new float3(0f, 1f, 0f),
                ContactCount = 3,
                FeatureId0 = 11,
                FeatureId1 = 20,
                FeatureId2 = 10
            };

            constraint.MatchManifold(manifold);

            Assert.Equal(2f, constraint.NormalImpulse0);
            Assert.Equal(1f, constraint.NormalImpulse1);
            Assert.Equal(4f, constraint.NormalImpulse2);
            Assert.Equal(0f, constraint.NormalImpulse3);
        }

        /// <summary>
        /// Ensures a materially different normal still clears stale cached impulses.
        /// </summary>
        [Fact]
        public void MatchManifold_WithLargeNormalChange_ResetsCachedImpulses() {
            Entity firstEntity = new Entity();
            Entity secondEntity = new Entity();
            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(firstEntity, secondEntity) {
                NormalImpulse0 = 4f,
                NormalImpulse1 = 2f,
                TangentImpulse = new float3(0.5f, 0f, 0.25f),
                TwistImpulse = 0.4f,
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11,
                LastNormal = new float3(0f, 1f, 0f),
                HasLastNormal = true
            };
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = new float3(1f, 0f, 0f),
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11
            };

            constraint.MatchManifold(manifold);

            Assert.Equal(0f, constraint.NormalImpulse0);
            Assert.Equal(0f, constraint.NormalImpulse1);
            Assert.Equal(float3.Zero, constraint.TangentImpulse);
            Assert.Equal(0f, constraint.TwistImpulse);
        }
    }
}
