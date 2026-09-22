namespace helengine {
    /// <summary>
    /// Registers HelPhysics with the normal core scene lifecycle and generated runtime module bootstrap.
    /// </summary>
    public static class HelPhysicsRuntimeComponentRegistration {

        /// <summary>Resolves or creates the registration state stored on one core.</summary>
        /// <param name="core">Core whose state should be resolved.</param>
        /// <returns>Registration state owned by the core.</returns>
        [NativeBorrowedReturn]
        static HelPhysicsRuntimeRegistrationState3D GetRegistrationState(Core core) {
            ValidateCore(core);
            if (core.PhysicsRuntimeRegistrationState is HelPhysicsRuntimeRegistrationState3D state) {
                return core.PhysicsRuntimeRegistrationState as HelPhysicsRuntimeRegistrationState3D;
            }
            NativeOwnership.DisposeAndRelease(ref core.PhysicsRuntimeRegistrationState);
            core.PhysicsRuntimeRegistrationState = new HelPhysicsRuntimeRegistrationState3D(core);
            return core.PhysicsRuntimeRegistrationState as HelPhysicsRuntimeRegistrationState3D;
        }

        /// <summary>Registers lazy scene lifecycle callbacks without creating a runtime for physics-free scenes.</summary>
        /// <param name="core">Core whose scene manager should be observed.</param>
        public static void Register(Core core) {
            HelPhysicsRuntimeRegistrationState3D state = GetRegistrationState(core);
            RegisterSceneBinding(core, state);
        }

        /// <summary>Creates one core-owned HelPhysics runtime whose fixed step matches the core scheduler.</summary>
        /// <param name="core">Core supplying the fixed-step scheduler.</param>
        /// <returns>A newly constructed HelPhysics runtime adapter.</returns>
        public static HelPhysicsRuntime3D CreateRuntimeWorld(Core core) {
            ValidateCore(core);
            return HelPhysicsRuntimeFactory3D.CreateForCore(core);
        }

        /// <summary>Attaches one supplied HelPhysics runtime to a core and retains ownership for scene transitions.</summary>
        /// <param name="core">Core that will own the runtime.</param>
        /// <param name="world">Runtime adapter to attach.</param>
        public static void AttachRuntimeWorld(Core core, [NativeTakesOwnership] HelPhysicsRuntime3D world) {
            ValidateCore(core);
            ValidateWorld(world);
            HelPhysicsRuntimeRegistrationState3D state = GetRegistrationState(core);
            ReplaceOwnedRuntimeWorld(state, world);
            core.AttachPhysicsRuntime(state.RuntimeWorld);
        }

        /// <summary>Subscribes one state to scene lifecycle events.</summary>
        /// <param name="core">Core whose scene manager should be observed.</param>
        /// <param name="state">State whose bound methods receive scene notifications.</param>
        static void RegisterSceneBinding(Core core, HelPhysicsRuntimeRegistrationState3D state) {
            ValidateCore(core);
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }
            if (core.SceneManager == null || state.SceneBindingRegistered) {
                return;
            }
            core.SceneManager.SceneLoaded += state.HandleSceneLoaded;
            core.SceneManager.SceneUnloading += state.HandleSceneUnloading;
            state.SceneBindingRegistered = true;
        }

        /// <summary>Unsubscribes lifecycle callbacks owned by one registration state.</summary>
        /// <param name="state">State whose callbacks should be removed.</param>
        internal static void UnregisterSceneBinding(HelPhysicsRuntimeRegistrationState3D state) {
            if (state == null || state.Core.SceneManager == null) {
                return;
            }
            if (state.SceneBindingRegistered) {
                state.Core.SceneManager.SceneLoaded -= state.HandleSceneLoaded;
                state.Core.SceneManager.SceneUnloading -= state.HandleSceneUnloading;
                state.SceneBindingRegistered = false;
            }
        }
        /// <summary>Replaces the runtime owned by one state after disposing the previous adapter.</summary>
        /// <param name="state">State whose runtime is being replaced.</param>
        /// <param name="replacementWorld">Replacement adapter, or null to release the current adapter.</param>
        static void ReplaceOwnedRuntimeWorld(HelPhysicsRuntimeRegistrationState3D state, [NativeTakesOwnership] HelPhysicsRuntime3D replacementWorld) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }
            HelPhysicsRuntime3D previousWorld = state.RuntimeWorld;
            if (ReferenceEquals(previousWorld, replacementWorld)) {
                return;
            }
            if (previousWorld != null && ReferenceEquals(state.Core.PhysicsRuntime, previousWorld)) {
                state.Core.DetachPhysicsRuntime();
            }
            NativeOwnership.DisposeAndRelease(ref state.RuntimeWorld);
            state.RuntimeWorld = replacementWorld;
        }

        /// <summary>Validates one core reference before registration proceeds.</summary>
        /// <param name="core">Core under validation.</param>
        static void ValidateCore(Core core) {
            if (core == null) {
                throw new ArgumentNullException(nameof(core));
            }
        }

        /// <summary>Validates one runtime adapter before attachment.</summary>
        /// <param name="world">Runtime adapter under validation.</param>
        static void ValidateWorld(HelPhysicsRuntime3D world) {
            if (world == null) {
                throw new ArgumentNullException(nameof(world));
            }
        }

        /// <summary>Binds one loaded hierarchy or releases the runtime for a physics-free scene.</summary>
        /// <param name="core">Core that owns the runtime state.</param>
        /// <param name="rootEntities">Loaded scene roots.</param>
        internal static void HandleLoadedScene(Core core, IReadOnlyList<Entity> rootEntities) {
            ValidateCore(core);
            ValidateRootEntities(rootEntities);
            HelPhysicsRuntimeRegistrationState3D state = GetRegistrationState(core);
            if (!SceneRequiresRuntime(rootEntities)) {
                ReleaseRuntime(state);
                return;
            }
            if (state.RuntimeWorld == null) {
                ReplaceOwnedRuntimeWorld(state, CreateRuntimeWorld(core));
            }
            bool runtimeBound = false;
            try {
                state.RuntimeWorld.BindScene(rootEntities);
                runtimeBound = true;
            } finally {
                if (!runtimeBound) {
                    ReleaseRuntime(state);
                }
            }
            if (!ReferenceEquals(core.PhysicsRuntime, state.RuntimeWorld)) {
                core.AttachPhysicsRuntime(state.RuntimeWorld);
            }
            core.ReportSceneTransitionStage("AfterHelPhysicsSceneBinding");
        }

        /// <summary>Releases the runtime before a physics scene hierarchy unloads.</summary>
        /// <param name="core">Core that owns the runtime state.</param>
        /// <param name="rootEntities">Scene roots being unloaded.</param>
        internal static void HandleUnloadingScene(Core core, IReadOnlyList<Entity> rootEntities) {
            ValidateCore(core);
            ValidateRootEntities(rootEntities);
            HelPhysicsRuntimeRegistrationState3D state = GetRegistrationState(core);
            if (state.RuntimeWorld == null || !SceneRequiresRuntime(rootEntities)) {
                return;
            }
            ReleaseRuntime(state);
        }

        /// <summary>Detaches and disposes the current runtime adapter.</summary>
        /// <param name="state">State whose runtime should be released.</param>
        static void ReleaseRuntime(HelPhysicsRuntimeRegistrationState3D state) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.RuntimeWorld != null && ReferenceEquals(state.Core.PhysicsRuntime, state.RuntimeWorld)) {
                state.Core.DetachPhysicsRuntime();
            }
            NativeOwnership.DisposeAndRelease(ref state.RuntimeWorld);
        }

        /// <summary>Determines whether one hierarchy contains authored physics components.</summary>
        /// <param name="rootEntities">Scene roots to inspect.</param>
        /// <returns>True when a physics runtime is required.</returns>
        static bool SceneRequiresRuntime(IReadOnlyList<Entity> rootEntities) {
            ValidateRootEntities(rootEntities);
            for (int rootIndex = 0; rootIndex < rootEntities.Count; rootIndex++) {
                if (EntityRequiresRuntime(rootEntities[rootIndex])) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Recursively detects authored physics components without ignoring malformed composition.</summary>
        /// <param name="entity">Entity subtree root to inspect.</param>
        /// <returns>True when the subtree contains a physics-owned component.</returns>
        static bool EntityRequiresRuntime(Entity entity) {
            if (entity == null) {
                return false;
            }
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    Component component = entity.Components[componentIndex];
                    if (component is RigidBody3DComponent || component is Collider3DComponent || component is CharacterController3DComponent || component is SceneEntityTriggerObserverComponent) {
                        return true;
                    }
                }
            }
            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    if (EntityRequiresRuntime(entity.Children[childIndex])) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Validates scene roots and rejects null entries before binding.</summary>
        /// <param name="rootEntities">Scene roots under validation.</param>
        static void ValidateRootEntities(IReadOnlyList<Entity> rootEntities) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            for (int index = 0; index < rootEntities.Count; index++) {
                if (rootEntities[index] == null) {
                    throw new ArgumentNullException(nameof(rootEntities), "Scene roots cannot contain null entities.");
                }
            }
        }
    }
}
