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
        /// Attaches the BEPU-backed runtime and scene-load binding on one initialized core instance.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene loader.</param>
        public static void Register(Core core) {
            ValidateCore(core);
            BepuPhysicsWorld3D world = CreateRuntimeWorld(core);
            AttachRuntimeWorld(core, world);
            RegisterSceneBinding(core);
        }

        /// <summary>
        /// Creates one BEPU-backed physics world for attachment to a runtime core.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime physics attachment.</param>
        /// <returns>Constructed BEPU-backed physics world.</returns>
        public static BepuPhysicsWorld3D CreateRuntimeWorld(Core core) {
            ValidateCore(core);
            return BepuPhysicsWorld3D.CreateDefault();
        }

        /// <summary>
        /// Attaches one created BEPU-backed physics world to the runtime core and stores it for scene-load rebinding.
        /// </summary>
        /// <param name="core">Initialized core that will own the physics runtime.</param>
        /// <param name="world">Constructed BEPU-backed physics world.</param>
        public static void AttachRuntimeWorld(Core core, BepuPhysicsWorld3D world) {
            ValidateCore(core);
            ValidateWorld(world);

            RuntimeWorld = world;
            core.AttachPhysicsRuntime(world);
        }

        /// <summary>
        /// Hooks the runtime scene manager so newly loaded scenes bind into the attached BEPU-backed physics world.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene manager.</param>
        public static void RegisterSceneBinding(Core core) {
            ValidateCore(core);

            if (core.SceneManager != null) {
                core.SceneManager.SceneLoaded += BindLoadedScene;
            }
        }

        /// <summary>
        /// Validates that one core instance is available before registration proceeds.
        /// </summary>
        /// <param name="core">Core instance under validation.</param>
        static void ValidateCore(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
        }

        /// <summary>
        /// Validates that one BEPU-backed world is available before runtime attachment proceeds.
        /// </summary>
        /// <param name="world">World instance under validation.</param>
        static void ValidateWorld(BepuPhysicsWorld3D world) {
            if (world == null) {
                throw new ArgumentNullException(nameof(world));
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
