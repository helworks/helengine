namespace helengine {
    /// <summary>
    /// Registers packaged scene component support required by the BEPU-backed 3D physics runtime.
    /// </summary>
    public static class BepuRuntimeComponentRegistration {
        /// <summary>
        /// Stores the last core instance that registered the BEPU-backed runtime hook.
        /// </summary>
        static Core RuntimeCore;

        /// <summary>
        /// Last default world attached through this registration hook; used by scene-load callbacks emitted by the active core.
        /// </summary>
        static BepuPhysicsWorld3D RuntimeWorld;

        /// <summary>
        /// Hooks scene-load binding on one initialized core instance and defers BEPU-backed runtime attachment until one supported physics scene loads.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene loader.</param>
        public static void Register(Core core) {
            ValidateCore(core);
            RuntimeCore = core;
            RuntimeWorld = core.PhysicsRuntime as BepuPhysicsWorld3D;
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
                RuntimeCore = Core.Instance;
            }

            HandleLoadedScene(Core.Instance, eventArgs.RootEntities);
        }

        /// <summary>
        /// Applies lazy runtime attachment and scene binding for one loaded scene hierarchy.
        /// </summary>
        /// <param name="core">Initialized core that owns the runtime scene loader.</param>
        /// <param name="rootEntities">Materialized root entities that may require BEPU-backed simulation.</param>
        internal static void HandleLoadedScene(Core core, IReadOnlyList<Entity> rootEntities) {
            ValidateCore(core);
            ValidateRootEntities(rootEntities);

            RuntimeCore = core;
            if (!SceneRequiresRuntime(rootEntities)) {
                DetachRuntimeWorld(core);
                return;
            }

            BepuPhysicsWorld3D world = EnsureRuntimeWorldAttached(core);
            world.BindScene(rootEntities);
        }

        /// <summary>
        /// Ensures one BEPU-backed runtime world exists and is attached to the supplied core before scene binding proceeds.
        /// </summary>
        /// <param name="core">Initialized core that should own the BEPU-backed runtime.</param>
        /// <returns>Attached BEPU-backed runtime world.</returns>
        static BepuPhysicsWorld3D EnsureRuntimeWorldAttached(Core core) {
            ValidateCore(core);

            if (RuntimeWorld == null) {
                RuntimeWorld = CreateRuntimeWorld(core);
            }
            if (!ReferenceEquals(core.PhysicsRuntime, RuntimeWorld)) {
                AttachRuntimeWorld(core, RuntimeWorld);
            }

            return RuntimeWorld;
        }

        /// <summary>
        /// Detaches the currently attached BEPU-backed runtime when one loaded scene does not require physics simulation.
        /// </summary>
        /// <param name="core">Initialized core that currently owns the runtime attachment.</param>
        static void DetachRuntimeWorld(Core core) {
            ValidateCore(core);

            if (ReferenceEquals(core.PhysicsRuntime, RuntimeWorld)) {
                core.DetachPhysicsRuntime();
            }
        }

        /// <summary>
        /// Determines whether one loaded scene hierarchy contains authored components handled by the BEPU-backed runtime.
        /// </summary>
        /// <param name="rootEntities">Materialized root entities emitted by the scene manager.</param>
        /// <returns>True when the scene requires BEPU-backed simulation.</returns>
        static bool SceneRequiresRuntime(IReadOnlyList<Entity> rootEntities) {
            ValidateRootEntities(rootEntities);

            for (int index = 0; index < rootEntities.Count; index++) {
                if (EntityRequiresRuntime(rootEntities[index])) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether one entity subtree contains authored rigid-body and collider components handled by the BEPU-backed runtime.
        /// </summary>
        /// <param name="entity">Entity subtree root under inspection.</param>
        /// <returns>True when the entity subtree requires BEPU-backed simulation.</returns>
        static bool EntityRequiresRuntime(Entity entity) {
            if (entity == null) {
                return false;
            }

            bool hasRigidBody = false;
            bool hasSupportedCollider = false;
            List<Component> components = entity.Components;
            if (components != null) {
                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++) {
                    Component component = components[componentIndex];
                    if (component is RigidBody3DComponent) {
                        hasRigidBody = true;
                    } else if (component is BoxCollider3DComponent || component is SphereCollider3DComponent || component is StaticMeshCollider3DComponent) {
                        hasSupportedCollider = true;
                    }
                }
            }

            if (hasRigidBody && hasSupportedCollider) {
                return true;
            }

            List<Entity> children = entity.Children;
            if (children == null) {
                return false;
            }

            for (int childIndex = 0; childIndex < children.Count; childIndex++) {
                if (EntityRequiresRuntime(children[childIndex])) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validates that one loaded scene hierarchy is available before lazy runtime attachment proceeds.
        /// </summary>
        /// <param name="rootEntities">Loaded scene hierarchy under validation.</param>
        static void ValidateRootEntities(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
        }
    }
}
