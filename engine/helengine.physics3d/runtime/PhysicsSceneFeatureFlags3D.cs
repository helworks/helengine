namespace helengine {
    /// <summary>
    /// Identifies the runtime 3D physics systems and interaction paths one scene can possibly require.
    /// </summary>
    [Flags]
    public enum PhysicsSceneFeatureFlags3D : uint {
        /// <summary>
        /// No physics features are required by the scene.
        /// </summary>
        None = 0,

        /// <summary>
        /// At least one kinematic motion path is present.
        /// </summary>
        KinematicMotion = 1 << 0,

        /// <summary>
        /// At least one trigger collider is present.
        /// </summary>
        TriggerEvents = 1 << 1,

        /// <summary>
        /// The character-controller runtime path is required.
        /// </summary>
        CharacterController = 1 << 2,

        /// <summary>
        /// Dynamic box-to-box contact can occur.
        /// </summary>
        BoxBoxContact = 1 << 3,

        /// <summary>
        /// Dynamic sphere-to-sphere contact can occur.
        /// </summary>
        SphereSphereContact = 1 << 4,

        /// <summary>
        /// Dynamic sphere-to-box contact can occur.
        /// </summary>
        SphereBoxContact = 1 << 5,

        /// <summary>
        /// Dynamic capsule-to-box contact can occur.
        /// </summary>
        CapsuleBoxContact = 1 << 6,

        /// <summary>
        /// Dynamic capsule-to-sphere contact can occur.
        /// </summary>
        CapsuleSphereContact = 1 << 7,

        /// <summary>
        /// Dynamic capsule-to-capsule contact can occur.
        /// </summary>
        CapsuleCapsuleContact = 1 << 8,

        /// <summary>
        /// Dynamic box-to-static-mesh contact can occur.
        /// </summary>
        BoxStaticMeshContact = 1 << 9,

        /// <summary>
        /// Dynamic sphere-to-static-mesh contact can occur.
        /// </summary>
        SphereStaticMeshContact = 1 << 10,

        /// <summary>
        /// Dynamic capsule-to-static-mesh contact can occur.
        /// </summary>
        CapsuleStaticMeshContact = 1 << 11,

        /// <summary>
        /// Character controllers can sample rigid-body support surfaces.
        /// </summary>
        CharacterControllerBodySupport = 1 << 12,

        /// <summary>
        /// Character controllers can sample cooked static-mesh support surfaces.
        /// </summary>
        CharacterControllerStaticMeshSupport = 1 << 13
    }
}
