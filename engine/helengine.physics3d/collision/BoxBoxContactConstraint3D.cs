namespace helengine {
    /// <summary>
    /// Stores warm-started accumulated impulses for one persistent box-box contact manifold.
    /// </summary>
    public sealed class BoxBoxContactConstraint3D {
        /// <summary>
        /// Initializes a persistent contact constraint for one ordered pair of entity-backed bodies.
        /// </summary>
        /// <param name="firstEntity">First entity participating in the contact pair.</param>
        /// <param name="secondEntity">Second entity participating in the contact pair.</param>
        public BoxBoxContactConstraint3D(Entity firstEntity, Entity secondEntity) {
            FirstEntity = firstEntity ?? throw new ArgumentNullException(nameof(firstEntity));
            SecondEntity = secondEntity ?? throw new ArgumentNullException(nameof(secondEntity));
        }

        /// <summary>
        /// Gets the first entity participating in the cached pair.
        /// </summary>
        public Entity FirstEntity { get; }

        /// <summary>
        /// Gets the second entity participating in the cached pair.
        /// </summary>
        public Entity SecondEntity { get; }

        /// <summary>
        /// Gets or sets whether the current physics step saw this contact pair.
        /// </summary>
        public bool WasTouchedThisStep { get; set; }

        /// <summary>
        /// Gets or sets whether the cached impulses were already applied during this step.
        /// </summary>
        public bool WasWarmStartedThisStep { get; set; }

        /// <summary>
        /// Gets or sets the accumulated normal impulse for the first manifold contact.
        /// </summary>
        public float NormalImpulse0 { get; set; }

        /// <summary>
        /// Gets or sets the accumulated normal impulse for the second manifold contact.
        /// </summary>
        public float NormalImpulse1 { get; set; }

        /// <summary>
        /// Gets or sets the accumulated normal impulse for the third manifold contact.
        /// </summary>
        public float NormalImpulse2 { get; set; }

        /// <summary>
        /// Gets or sets the accumulated normal impulse for the fourth manifold contact.
        /// </summary>
        public float NormalImpulse3 { get; set; }

        /// <summary>
        /// Gets or sets the positive normal impulse applied to the first contact during the current step.
        /// </summary>
        public float FrameNormalImpulse0 { get; set; }

        /// <summary>
        /// Gets or sets the positive normal impulse applied to the second contact during the current step.
        /// </summary>
        public float FrameNormalImpulse1 { get; set; }

        /// <summary>
        /// Gets or sets the positive normal impulse applied to the third contact during the current step.
        /// </summary>
        public float FrameNormalImpulse2 { get; set; }

        /// <summary>
        /// Gets or sets the positive normal impulse applied to the fourth contact during the current step.
        /// </summary>
        public float FrameNormalImpulse3 { get; set; }

        /// <summary>
        /// Gets or sets the accumulated world-space tangent friction impulse for the manifold.
        /// </summary>
        public float3 TangentImpulse { get; set; }

        /// <summary>
        /// Gets or sets the accumulated twist friction impulse around the contact normal.
        /// </summary>
        public float TwistImpulse { get; set; }

        /// <summary>
        /// Gets or sets the feature id previously stored for the first manifold contact.
        /// </summary>
        public int FeatureId0 { get; set; }

        /// <summary>
        /// Gets or sets the feature id previously stored for the second manifold contact.
        /// </summary>
        public int FeatureId1 { get; set; }

        /// <summary>
        /// Gets or sets the feature id previously stored for the third manifold contact.
        /// </summary>
        public int FeatureId2 { get; set; }

        /// <summary>
        /// Gets or sets the feature id previously stored for the fourth manifold contact.
        /// </summary>
        public int FeatureId3 { get; set; }

        /// <summary>
        /// Gets or sets the number of contacts stored during the previous solve.
        /// </summary>
        public int ContactCount { get; set; }

        /// <summary>
        /// Gets or sets the previous frame's contact normal.
        /// </summary>
        public float3 LastNormal { get; set; }

        /// <summary>
        /// Gets or sets whether a previous frame normal has been stored.
        /// </summary>
        public bool HasLastNormal { get; set; }

        /// <summary>
        /// Returns whether this constraint belongs to the supplied ordered entity pair.
        /// </summary>
        /// <param name="firstEntity">First entity to match.</param>
        /// <param name="secondEntity">Second entity to match.</param>
        /// <returns>True when both entity references match this constraint.</returns>
        public bool Matches(Entity firstEntity, Entity secondEntity) {
            return FirstEntity == firstEntity && SecondEntity == secondEntity;
        }

        /// <summary>
        /// Prepares the cached constraint for a new physics step.
        /// </summary>
        public void BeginStep() {
            WasTouchedThisStep = false;
            WasWarmStartedThisStep = false;
            NormalImpulse0 = NormalImpulse0 * 0.95f;
            NormalImpulse1 = NormalImpulse1 * 0.95f;
            NormalImpulse2 = NormalImpulse2 * 0.95f;
            NormalImpulse3 = NormalImpulse3 * 0.95f;
            FrameNormalImpulse0 = 0f;
            FrameNormalImpulse1 = 0f;
            FrameNormalImpulse2 = 0f;
            FrameNormalImpulse3 = 0f;
            TangentImpulse = TangentImpulse * 0.95f;
            TwistImpulse = TwistImpulse * 0.95f;
        }

        /// <summary>
        /// Clears cached impulses when the manifold contact identity changes.
        /// </summary>
        public void ResetImpulses() {
            NormalImpulse0 = 0f;
            NormalImpulse1 = 0f;
            NormalImpulse2 = 0f;
            NormalImpulse3 = 0f;
            FrameNormalImpulse0 = 0f;
            FrameNormalImpulse1 = 0f;
            FrameNormalImpulse2 = 0f;
            FrameNormalImpulse3 = 0f;
            TangentImpulse = float3.Zero;
            TwistImpulse = 0f;
        }

        /// <summary>
        /// Reinitializes cached impulses when the new manifold no longer matches the previous feature ids.
        /// </summary>
        /// <param name="manifold">Current box-box manifold.</param>
        public void MatchManifold(BoxBoxContactManifold3D manifold) {
            if (ContactCount != manifold.ContactCount ||
                HasLastNormal && float3.Dot(LastNormal, manifold.Normal) < 0.99f ||
                FeatureId0 != manifold.FeatureId0 ||
                FeatureId1 != manifold.FeatureId1 ||
                FeatureId2 != manifold.FeatureId2 ||
                FeatureId3 != manifold.FeatureId3) {
                ResetImpulses();
            }

            ContactCount = manifold.ContactCount;
            FeatureId0 = manifold.FeatureId0;
            FeatureId1 = manifold.FeatureId1;
            FeatureId2 = manifold.FeatureId2;
            FeatureId3 = manifold.FeatureId3;
            LastNormal = manifold.Normal;
            HasLastNormal = true;
        }
    }
}
