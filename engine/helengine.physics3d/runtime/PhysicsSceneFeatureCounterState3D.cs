namespace helengine {
    /// <summary>
    /// Stores intermediate entity-shape counts used while analyzing one scene's required physics features.
    /// </summary>
    public sealed class PhysicsSceneFeatureCounterState3D {
        /// <summary>
        /// Gets or sets the number of dynamic rigid bodies that use box colliders.
        /// </summary>
        public int DynamicBoxCount { get; set; }

        /// <summary>
        /// Gets or sets the number of dynamic rigid bodies that use sphere colliders.
        /// </summary>
        public int DynamicSphereCount { get; set; }

        /// <summary>
        /// Gets or sets the number of dynamic rigid bodies that use capsule colliders.
        /// </summary>
        public int DynamicCapsuleCount { get; set; }

        /// <summary>
        /// Gets or sets the number of non-trigger rigid bodies that use box colliders.
        /// </summary>
        public int SolidBoxCount { get; set; }

        /// <summary>
        /// Gets or sets the number of non-trigger rigid bodies that use sphere colliders.
        /// </summary>
        public int SolidSphereCount { get; set; }

        /// <summary>
        /// Gets or sets the number of non-trigger rigid bodies that use capsule colliders.
        /// </summary>
        public int SolidCapsuleCount { get; set; }

        /// <summary>
        /// Gets or sets the number of non-trigger static-mesh colliders.
        /// </summary>
        public int SolidStaticMeshCount { get; set; }

        /// <summary>
        /// Gets or sets the number of static or kinematic non-trigger box rigid bodies that can act as controller supports.
        /// </summary>
        public int CharacterControllerBodySupportCount { get; set; }

        /// <summary>
        /// Gets or sets the number of non-trigger static-mesh colliders that can act as controller supports.
        /// </summary>
        public int CharacterControllerStaticMeshSupportCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether at least one kinematic motion path is present.
        /// </summary>
        public bool HasKinematicMotion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether at least one trigger collider is present.
        /// </summary>
        public bool HasTriggerCollider { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether at least one character controller is present.
        /// </summary>
        public bool HasCharacterController { get; set; }
    }
}
