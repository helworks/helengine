namespace helengine {
    /// <summary>
    /// Stores one compact differential-trace observation that can be compared across managed and native reduced-BEPU runs.
    /// </summary>
    public sealed class BepuDifferentialTraceRecord3D {
        /// <summary>
        /// Gets or sets the zero-based simulation frame number for this observation.
        /// </summary>
        public int Frame { get; set; }

        /// <summary>
        /// Gets or sets the simulation boundary represented by this observation.
        /// </summary>
        public BepuDifferentialTracePhase3D Phase { get; set; }

        /// <summary>
        /// Gets or sets the body handle that identifies the observed body across managed and native runs.
        /// </summary>
        public int BodyHandle { get; set; }

        /// <summary>
        /// Gets or sets the active-set body index associated with the observed body when one is available.
        /// </summary>
        public int BodyIndex { get; set; }

        /// <summary>
        /// Gets or sets the bundle index associated with the observation when one is available.
        /// </summary>
        public int BundleIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the constraint-batch index associated with the observation when one is available.
        /// </summary>
        public int ConstraintBatchIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the type-batch index associated with the observation when one is available.
        /// </summary>
        public int TypeBatchIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the body slot inside the current constraint or bundle when one is available.
        /// </summary>
        public int BodySlotIndex { get; set; } = -1;

        /// <summary>
        /// Gets or sets the encoded body-reference lane text when the observation came from one bundle-based phase.
        /// </summary>
        public string EncodedReferences { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the integration-mask lane text when the observation came from one masked bundle-based phase.
        /// </summary>
        public string IntegrationMask { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the position associated with the observation.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Gets or sets the orientation associated with the observation.
        /// </summary>
        public float4 Orientation { get; set; }

        /// <summary>
        /// Gets or sets the linear velocity associated with the observation.
        /// </summary>
        public float3 LinearVelocity { get; set; }

        /// <summary>
        /// Gets or sets the angular velocity associated with the observation.
        /// </summary>
        public float3 AngularVelocity { get; set; }
    }
}
