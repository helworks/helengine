namespace helengine {
    /// <summary>
    /// Owns HelPhysics runtime and scene callback state for one core instance.
    /// </summary>
    sealed class HelPhysicsRuntimeRegistrationState3D : IDisposable {
        /// <summary>Core whose scene lifecycle owns this state.</summary>
        internal readonly Core Core;
        /// <summary>Runtime adapter owned by this state.</summary>
        [NativeOwnedMember]
        internal HelPhysicsRuntime3D RuntimeWorld;
        /// <summary>Tracks whether this state has subscribed its bound scene handlers.</summary>
        internal bool SceneBindingRegistered;
        /// <summary>Marks whether state disposal has already run.</summary>
        bool IsDisposed;

        /// <summary>Initializes state for one core.</summary>
        /// <param name="core">Core that owns the registration.</param>
        internal HelPhysicsRuntimeRegistrationState3D(Core core) {
            Core = core ?? throw new ArgumentNullException(nameof(core));
        }

        /// <summary>Handles one loaded scene notification through this persistent registration state.</summary>
        /// <param name="sceneManager">Scene manager that emitted the notification.</param>
        /// <param name="eventArgs">Loaded-scene payload containing materialized root entities.</param>
        internal void HandleSceneLoaded(SceneManager sceneManager, SceneLoadedEventArgs eventArgs) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            }
            if (eventArgs == null) {
                throw new ArgumentNullException(nameof(eventArgs));
            }

            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(Core, eventArgs.RootEntities);
        }

        /// <summary>Handles one unloading scene notification through this persistent registration state.</summary>
        /// <param name="sceneManager">Scene manager that emitted the notification.</param>
        /// <param name="eventArgs">Unloading-scene payload containing still-live root entities.</param>
        internal void HandleSceneUnloading(SceneManager sceneManager, SceneUnloadingEventArgs eventArgs) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            }
            if (eventArgs == null) {
                throw new ArgumentNullException(nameof(eventArgs));
            }

            HelPhysicsRuntimeComponentRegistration.HandleUnloadingScene(Core, eventArgs.RootEntities);
        }

        /// <summary>Unsubscribes callbacks, detaches the runtime, and releases the owned adapter.</summary>
        public void Dispose() {
            if (!IsDisposed) {
                IsDisposed = true;
                HelPhysicsRuntimeComponentRegistration.UnregisterSceneBinding(this);
                if (RuntimeWorld != null && ReferenceEquals(Core.PhysicsRuntime, RuntimeWorld)) {
                    Core.DetachPhysicsRuntime();
                }


            }

            NativeOwnership.DisposeAndRelease(ref RuntimeWorld);
        }
    }
}
