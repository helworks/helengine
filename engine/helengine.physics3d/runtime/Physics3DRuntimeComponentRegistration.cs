namespace helengine {
    /// <summary>
    /// Registers packaged scene component support required by the default 3D physics runtime.
    /// </summary>
    public static class Physics3DRuntimeComponentRegistration {
        /// <summary>
        /// Registers the 3D physics packaged-scene component deserializers on one initialized core instance.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene loader.</param>
        public static void Register(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }

            core.RegisterRuntimeComponentDeserializer(new RuntimeRigidBody3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeBoxCollider3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeSphereCollider3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeCapsuleCollider3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeStaticMeshCollider3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeKinematicMotion3DComponentDeserializer());
            core.RegisterRuntimeComponentDeserializer(new RuntimeCharacterController3DComponentDeserializer());
        }
    }
}
