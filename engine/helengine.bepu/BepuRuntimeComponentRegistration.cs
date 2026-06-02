namespace helengine {
    /// <summary>
    /// Registers packaged scene component support required by the BEPU-backed 3D physics runtime.
    /// </summary>
    public static class BepuRuntimeComponentRegistration {
        /// <summary>
        /// Last default world attached through this registration hook; used by scene-load callbacks emitted by the active core.
        /// </summary>
        static BepuPhysicsWorld3D RuntimeWorld;

        /// <summary>
        /// Registers the packaged-scene component deserializers on one initialized core instance and attaches the BEPU-backed runtime.
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

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            RuntimeWorld = world;
            core.AttachPhysicsRuntime(world);
            if (core.SceneManager != null) {
                core.SceneManager.SceneLoaded += BindLoadedScene;
            }
        }

        /// <summary>
        /// Rebinds the attached BEPU-backed physics world to the active scene hierarchy after the scene manager materializes a scene.
        /// </summary>
        /// <param name="sceneManager">Scene manager that emitted the load notification.</param>
        /// <param name="eventArgs">Scene load payload containing the materialized root entities.</param>
        static void BindLoadedScene(SceneManager sceneManager, SceneLoadedEventArgs eventArgs) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            }
            if (eventArgs == null) {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance is required before binding a loaded scene to the BEPU-backed 3D physics runtime.");
            }
            if (RuntimeWorld == null) {
                throw new InvalidOperationException("The BEPU-backed 3D physics world must be attached before binding loaded scenes.");
            }

            RuntimeWorld.BindScene(eventArgs.RootEntities);
        }
    }
}
