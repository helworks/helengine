namespace helengine {
    /// <summary>
    /// Registers configured HelPhysics scene binding and runtime attachment with one initialized Core instance.
    /// </summary>
    public static class HelPhysicsRuntimeComponentRegistration {
        /// <summary>
        /// Stores the Core whose scene manager currently owns the static registration callbacks.
        /// </summary>
        static Core RuntimeCore;

        /// <summary>
        /// Stores the immutable world profile used to create a fresh binder for each compatible loaded scene.
        /// </summary>
        static HelPhysicsWorldSettings3D RuntimeSettings;

        /// <summary>
        /// Stores the adapter attached for the currently loaded HelPhysics scene, or null when detached.
        /// </summary>
        static HelPhysicsSceneRuntime3D RuntimeValue;

        /// <summary>
        /// Gets the adapter retained by registration for lifecycle cleanup validation and retry handling.
        /// </summary>
        internal static HelPhysicsSceneRuntime3D RegisteredRuntime => RuntimeValue;

        /// <summary>
        /// Subscribes one initialized Core to configured HelPhysics scene loading without attaching a backend prematurely.
        /// </summary>
        /// <param name="core">Initialized Core that owns scene loading and fixed-step scheduling.</param>
        /// <param name="settings">Explicit immutable HelPhysics capacity, solve, gravity, and fixed-step profile.</param>
        /// <exception cref="ArgumentNullException">Thrown when either required argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the Core has no initialized scene manager.</exception>
        public static void RegisterConfigured(Core core, HelPhysicsWorldSettings3D settings) {
            ValidateCore(core);
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (core.SceneManager == null) {
                throw new InvalidOperationException("HelPhysics runtime registration requires an initialized Core scene manager.");
            }

            UnregisterCurrentCore();
            RuntimeCore = core;
            RuntimeSettings = settings;
            RuntimeValue = null;
            RuntimeCore.SceneManager.SceneLoaded += BindLoadedScene;
            RuntimeCore.SceneManager.SceneUnloading += DetachUnloadingScene;
        }

        /// <summary>
        /// Detaches runtime state and releases scene callbacks and static references owned for one registered Core.
        /// </summary>
        /// <param name="core">Exact registered Core whose HelPhysics lifecycle ownership should end.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="core"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the supplied Core does not own the current registration.</exception>
        public static void UnregisterConfigured(Core core) {
            ValidateRegisteredCore(core);
            UnregisterCurrentCore();
        }

        /// <summary>
        /// Creates, validates, binds, and attaches a fresh HelPhysics runtime for one complete loaded scene.
        /// </summary>
        /// <param name="core">Registered Core that owns the loaded scene.</param>
        /// <param name="rootEntities">Complete materialized scene roots in deterministic order.</param>
        internal static void HandleLoadedScene(Core core, IReadOnlyList<Entity> rootEntities) {
            ValidateRegisteredCore(core);
            ValidateRootEntities(rootEntities);
            DetachRuntime(core);
            if (!SceneRequiresRuntime(rootEntities)) {
                return;
            }

            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(RuntimeSettings);
            binder.BindHierarchies(rootEntities);
            HelPhysicsSceneRuntime3D runtime = new HelPhysicsSceneRuntime3D(binder);
            core.AttachPhysicsRuntime(runtime);
            RuntimeValue = runtime;
            core.ReportSceneTransitionStage("AfterHelPhysicsSceneBinding");
        }

        /// <summary>
        /// Detaches the active HelPhysics adapter before the scene manager disposes any binding-owned entity state.
        /// </summary>
        /// <param name="core">Registered Core that owns the unloading scene.</param>
        /// <param name="rootEntities">Still-live scene roots supplied by the unload notification.</param>
        internal static void HandleUnloadingScene(Core core, IReadOnlyList<Entity> rootEntities) {
            ValidateRegisteredCore(core);
            ValidateRootEntities(rootEntities);
            DetachRuntime(core);
        }

        /// <summary>
        /// Handles a registered scene manager's loaded-scene notification using the stored Core identity.
        /// </summary>
        /// <param name="sceneManager">Registered scene manager that emitted the notification.</param>
        /// <param name="eventArgs">Payload containing all materialized scene roots.</param>
        static void BindLoadedScene(SceneManager sceneManager, SceneLoadedEventArgs eventArgs) {
            ValidateSceneEvent(sceneManager, eventArgs);
            HandleLoadedScene(RuntimeCore, eventArgs.RootEntities);
        }

        /// <summary>
        /// Handles a registered scene manager's pre-disposal unload notification using the stored Core identity.
        /// </summary>
        /// <param name="sceneManager">Registered scene manager that emitted the notification.</param>
        /// <param name="eventArgs">Payload containing all still-live scene roots.</param>
        static void DetachUnloadingScene(SceneManager sceneManager, SceneUnloadingEventArgs eventArgs) {
            ValidateSceneEvent(sceneManager, eventArgs);
            HandleUnloadingScene(RuntimeCore, eventArgs.RootEntities);
        }

        /// <summary>
        /// Removes callbacks and a currently attached adapter from the prior Core before registration ownership changes.
        /// </summary>
        static void UnregisterCurrentCore() {
            if (RuntimeCore == null) {
                return;
            }

            if (RuntimeCore.SceneManager != null) {
                RuntimeCore.SceneManager.SceneLoaded -= BindLoadedScene;
                RuntimeCore.SceneManager.SceneUnloading -= DetachUnloadingScene;
            }
            DetachRuntime(RuntimeCore);
            RuntimeCore = null;
            RuntimeSettings = null;
        }

        /// <summary>
        /// Detaches only the exact adapter owned by this registration and clears its active-scene reference.
        /// </summary>
        /// <param name="core">Core that may currently own the registered adapter.</param>
        static void DetachRuntime(Core core) {
            HelPhysicsSceneRuntime3D runtime = RuntimeValue;
            if (runtime != null && ReferenceEquals(core.PhysicsRuntime, runtime)) {
                core.DetachPhysicsRuntime();
            }

            if (runtime != null) {
                runtime.Binder.UnbindAll();
            }

            RuntimeValue = null;
        }

        /// <summary>
        /// Determines whether a scene contains any authored 3D physics component that HelPhysics must either bind or reject.
        /// </summary>
        /// <param name="rootEntities">Validated scene roots under inspection.</param>
        /// <returns>True when any entity contains a rigid body or collider; otherwise false.</returns>
        static bool SceneRequiresRuntime(IReadOnlyList<Entity> rootEntities) {
            for (int rootIndex = 0; rootIndex < rootEntities.Count; rootIndex++) {
                if (EntityRequiresRuntime(rootEntities[rootIndex])) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Recursively determines whether one entity hierarchy contains any authored 3D physics component.
        /// </summary>
        /// <param name="entity">Validated hierarchy root under inspection.</param>
        /// <returns>True when this entity or a descendant contains a rigid body or collider.</returns>
        static bool EntityRequiresRuntime(Entity entity) {
            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                Component component = entity.Components[componentIndex];
                if (component is RigidBody3DComponent || component is Collider3DComponent) {
                    return true;
                }
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                if (EntityRequiresRuntime(entity.Children[childIndex])) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validates that one Core reference is available.
        /// </summary>
        /// <param name="core">Core reference under validation.</param>
        static void ValidateCore(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
        }

        /// <summary>
        /// Validates that one Core is the exact instance currently owning HelPhysics registration.
        /// </summary>
        /// <param name="core">Core reference under validation.</param>
        static void ValidateRegisteredCore(Core core) {
            ValidateCore(core);
            if (!ReferenceEquals(core, RuntimeCore) || RuntimeSettings == null) {
                throw new InvalidOperationException("HelPhysics scene handling requires the configured registered Core instance.");
            }
        }

        /// <summary>
        /// Validates one loaded or unloading root collection before runtime state changes.
        /// </summary>
        /// <param name="rootEntities">Scene roots under validation.</param>
        static void ValidateRootEntities(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }

            for (int rootIndex = 0; rootIndex < rootEntities.Count; rootIndex++) {
                Entity root = rootEntities[rootIndex];
                if (root == null) {
                    throw new ArgumentNullException(nameof(rootEntities), "A HelPhysics scene root cannot be null.");
                }
                if (root.Components == null || root.Children == null) {
                    throw new InvalidOperationException("HelPhysics scene roots require initialized component and child collections.");
                }
            }
        }

        /// <summary>
        /// Validates that an event came from the registered scene manager with a non-null payload.
        /// </summary>
        /// <param name="sceneManager">Scene manager that emitted the event.</param>
        /// <param name="eventArgs">Loaded or unloading scene payload.</param>
        static void ValidateSceneEvent(SceneManager sceneManager, object eventArgs) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            }
            if (eventArgs == null) {
                throw new ArgumentNullException(nameof(eventArgs));
            }
            if (RuntimeCore == null || !ReferenceEquals(sceneManager, RuntimeCore.SceneManager)) {
                throw new InvalidOperationException("HelPhysics received a scene event from an unregistered scene manager.");
            }
        }
    }
}
