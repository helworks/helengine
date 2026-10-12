namespace helengine {
    /// <summary>Validates native module dependencies and retains code until every scene, callback and component factory using it has been released.</summary>
    public sealed class SceneModuleManager : IDisposable {
        /// <summary>Borrowed core that owns the runtime services used during module registration.</summary>
        readonly Core OwnerCore;
        /// <summary>Borrowed provider whose adapter code is resident throughout the runtime session.</summary>
        readonly IRuntimeCodeModuleProvider Provider;
        /// <summary>Borrowed component registry whose module-owned factories are removed before code unloading.</summary>
        readonly RuntimeComponentRegistry Registry;
        /// <summary>Borrowed immutable scene dependency catalog.</summary>
        readonly RuntimeSceneCatalog SceneCatalog;
        /// <summary>Owned state records in manifest order; their entries remain borrowed.</summary>
        [NativeOwnedMember]
        RuntimeCodeModuleState[] States;
        /// <summary>Owned map of scene leases; integer arrays identify deduplicated dependency closures.</summary>
        [NativeOwnedMember]
        Dictionary<string, int[]> SceneLeases;
        /// <summary>Prevents operations from using native module state after final teardown.</summary>
        bool IsDisposed;

        /// <summary>Validates the complete manifest and all scene dependencies before any native loading starts.</summary>
        /// <param name="core">Owning runtime core.</param>
        /// <param name="manifest">Exact packaged native residency and dependency declarations.</param>
        /// <param name="provider">Resident platform code loader.</param>
        /// <param name="registry">Owning component factory registry.</param>
        /// <param name="sceneCatalog">Exact scene-to-module dependency declarations.</param>
        public SceneModuleManager(Core core, RuntimeCodeModuleManifest manifest, IRuntimeCodeModuleProvider provider, RuntimeComponentRegistry registry, RuntimeSceneCatalog sceneCatalog) {
            OwnerCore = core ?? throw new ArgumentNullException(nameof(core));
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            SceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
            if (manifest == null) { throw new ArgumentNullException(nameof(manifest)); }
            States = new RuntimeCodeModuleState[manifest.Entries.Length];
            SceneLeases = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < States.Length; index++) {
                RuntimeCodeModuleManifestEntry entry = manifest.Entries[index];
                if (entry == null) { throw new InvalidOperationException("Code module manifest entries cannot be null."); }
                for (int previous = 0; previous < index; previous++) {
                    if (string.Equals(States[previous].Entry.ModuleId, entry.ModuleId, StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"Duplicate native code module '{entry.ModuleId}'.");
                    }
                }
                if (entry.LoadState != RuntimeCodeModuleLoadState.ResidentAtStartup && entry.LoadState != RuntimeCodeModuleLoadState.SceneResident && entry.LoadState != RuntimeCodeModuleLoadState.Unloadable) {
                    throw new InvalidOperationException($"Invalid residency state for native code module '{entry.ModuleId}'.");
                }
                States[index] = new RuntimeCodeModuleState(entry);
            }
            List<int> order = new List<int>();
            int[] marks = new int[States.Length];
            try {
                for (int index = 0; index < States.Length; index++) { VisitDependency(index, marks, order); }
                for (int scene = 0; scene < SceneCatalog.Entries.Length; scene++) {
                    string[] required = SceneCatalog.Entries[scene].RequiredCodeModuleIds;
                    for (int dependency = 0; dependency < required.Length; dependency++) { FindModuleIndex(required[dependency]); }
                }
            } finally {
                NativeOwnership.Delete(order);
                NativeOwnership.Delete(marks);
            }
        }

        /// <summary>Gets how many distinct native modules currently have loaded code.</summary>
        public int LoadedModuleCount {
            get {
                int count = 0;
                for (int index = 0; index < States.Length; index++) { if (States[index].Module != null) { count++; } }
                return count;
            }
        }

        /// <summary>Begins one owned scene acquisition containing resident modules and its complete deduplicated dependency closure.</summary>
        /// <param name="sceneId">Exact scene identity being prepared.</param>
        /// <returns>Owned acquisition whose disposal reverses all uncommitted references.</returns>
        [NativeOwnedReturn]
        public SceneModuleLoadOperation BeginAcquireScene(string sceneId) {
            if (IsDisposed) { throw new InvalidOperationException("Disposed module managers cannot acquire scenes."); }
            if (SceneLeases.ContainsKey(sceneId)) { throw new InvalidOperationException($"Scene '{sceneId}' already holds a code module lease."); }
            if (!SceneCatalog.TryGetEntry(sceneId, out RuntimeSceneCatalogEntry scene)) { throw new InvalidOperationException($"Scene '{sceneId}' is absent from the native scene catalog."); }
            List<int> order = new List<int>();
            int[] marks = new int[States.Length];
            try {
                for (int index = 0; index < States.Length; index++) {
                    if (States[index].Entry.LoadState == RuntimeCodeModuleLoadState.ResidentAtStartup) { VisitDependency(index, marks, order); }
                }
                for (int index = 0; index < scene.RequiredCodeModuleIds.Length; index++) { VisitDependency(FindModuleIndex(scene.RequiredCodeModuleIds[index]), marks, order); }
                int[] indices = new int[order.Count];
                for (int index = 0; index < order.Count; index++) { indices[index] = order[index]; }
                return new SceneModuleLoadOperation(this, Provider, sceneId, indices);
            } finally {
                NativeOwnership.Delete(order);
                NativeOwnership.Delete(marks);
            }
        }

        /// <summary>Gets a borrowed validated state record used by the scene acquisition cursor.</summary>
        /// <param name="index">Manifest-order state index.</param>
        /// <returns>Borrowed state, valid until manager disposal.</returns>
        [NativeBorrowedReturn]
        internal RuntimeCodeModuleState GetState(int index) { return States[index]; }

        /// <summary>Registers a newly loaded handle, reversing partial registration before releasing code if registration fails.</summary>
        /// <param name="index">Manifest-order module index.</param>
        /// <param name="module">Owned handle transferred from the platform load operation.</param>
        internal void InstallModule(int index, [NativeTakesOwnership] IRuntimeCodeModule module) {
            if (module == null) { throw new InvalidOperationException("Native module loaders must return a ready module handle."); }
            RuntimeCodeModuleState state = States[index];
            if (state.Module != null) { throw new InvalidOperationException("A native module handle is already installed."); }
            NativeOwnership.Release(ref state.Module);
            state.Module = module;
            bool registered = false;
            try {
                module.Register(OwnerCore, Registry);
                registered = true;
            } finally {
                if (!registered) { UnloadModule(index); }
            }
        }

        /// <summary>Copies the completed acquisition's deduplicated closure into the loaded scene lease table.</summary>
        /// <param name="sceneId">Scene receiving code lifetime ownership.</param>
        /// <param name="indices">Borrowed dependency closure copied before the acquisition releases its metadata.</param>
        internal void CommitSceneLease(string sceneId, [NativeNoEscape] int[] indices) {
            int[] copiedIndices = new int[indices.Length];
            for (int index = 0; index < indices.Length; index++) { copiedIndices[index] = indices[index]; }
            SceneLeases.Add(sceneId, copiedIndices);
        }

        /// <summary>Reports whether a tracked scene owns a module closure, excluding externally tracked monolithic scenes.</summary>
        /// <param name="sceneId">Scene identity.</param>
        /// <returns>True when release is required after scene teardown.</returns>
        public bool HasSceneLease(string sceneId) { return SceneLeases.ContainsKey(sceneId); }

        /// <summary>Releases one module reference after all referring scene objects have been destroyed.</summary>
        /// <param name="index">Manifest-order module index.</param>
        internal void ReleaseReference(int index) {
            RuntimeCodeModuleState state = States[index];
            if (state.ReferenceCount <= 0) { throw new InvalidOperationException("Native module reference count cannot underflow."); }
            state.ReferenceCount--;
            if (state.ReferenceCount == 0 && !IsResidentDependency(index)) { UnloadModule(index); }
        }

        /// <summary>Releases a scene's code in reverse dependency order after scene disposal, notification and GPU retirement.</summary>
        /// <param name="sceneId">Loaded scene whose remaining code references are being released.</param>
        public void ReleaseScene(string sceneId) {
            if (!SceneLeases.TryGetValue(sceneId, out int[] indices)) { throw new InvalidOperationException($"Scene '{sceneId}' has no native module lease."); }
            for (int index = indices.Length - 1; index >= 0; index--) { ReleaseReference(indices[index]); }
            SceneLeases.Remove(sceneId);
        }

        /// <summary>Determines whether a module is pinned directly or transitively by a permanently resident declaration.</summary>
        /// <param name="candidate">Manifest-order module being considered for unloading.</param>
        /// <returns>True when code must survive scene lease release.</returns>
        bool IsResidentDependency(int candidate) {
            for (int index = 0; index < States.Length; index++) {
                if (States[index].Entry.LoadState == RuntimeCodeModuleLoadState.ResidentAtStartup && DependsOn(index, candidate)) { return true; }
            }
            return false;
        }

        /// <summary>Traverses an already validated acyclic dependency chain to identify transitive resident pins.</summary>
        /// <param name="index">Current dependency node.</param>
        /// <param name="candidate">Target module being tested.</param>
        /// <returns>True when the current node equals or depends on the target module.</returns>
        bool DependsOn(int index, int candidate) {
            if (index == candidate) { return true; }
            string[] dependencies = States[index].Entry.DependencyModuleIds;
            for (int dependency = 0; dependency < dependencies.Length; dependency++) {
                if (DependsOn(FindModuleIndex(dependencies[dependency]), candidate)) { return true; }
            }
            return false;
        }

        /// <summary>Builds a dependency-first closure and reports cycles before any platform allocation or registration occurs.</summary>
        /// <param name="index">Manifest node being visited.</param>
        /// <param name="marks">Owned traversal marks: unseen, active and complete.</param>
        /// <param name="order">Owned ordered closure receiving each module once.</param>
        void VisitDependency(int index, [NativeNoEscape] int[] marks, [NativeNoEscape] List<int> order) {
            if (marks[index] == 1) { throw new InvalidOperationException($"Native code module dependency cycle at '{States[index].Entry.ModuleId}'."); }
            if (marks[index] == 2) { return; }
            marks[index] = 1;
            string[] dependencies = States[index].Entry.DependencyModuleIds;
            for (int dependency = 0; dependency < dependencies.Length; dependency++) { VisitDependency(FindModuleIndex(dependencies[dependency]), marks, order); }
            marks[index] = 2;
            order.Add(index);
        }

        /// <summary>Finds an exact declared module identity without inventing an undeclared fallback dependency.</summary>
        /// <param name="moduleId">Packaged module identity.</param>
        /// <returns>Manifest-order module index.</returns>
        int FindModuleIndex(string moduleId) {
            for (int index = 0; index < States.Length; index++) {
                if (string.Equals(States[index].Entry.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase)) { return index; }
            }
            throw new InvalidOperationException($"Required native code module '{moduleId}' is absent from the manifest.");
        }

        /// <summary>Detaches callbacks, destroys registry-owned factories and only then finalizes and frees executable code.</summary>
        /// <param name="index">Manifest-order loaded module index.</param>
        void UnloadModule(int index) {
            RuntimeCodeModuleState state = States[index];
            if (state.Module == null) { return; }
            state.Module.Unregister(OwnerCore, Registry);
            Registry.UnregisterModule(state.Entry.ModuleId);
            NativeOwnership.DisposeAndRelease(ref state.Module);
        }

        /// <summary>Releases resident code only after every scene lease has been retired; refuses disposal while scene objects can still execute module methods.</summary>
        public void Dispose() {
            if (IsDisposed) {
                NativeOwnership.Release(ref States);
                NativeOwnership.Release(ref SceneLeases);
                return;
            }
            if (SceneLeases.Count != 0) { throw new InvalidOperationException("Scene code leases must be released before disposing their module manager."); }
            for (int index = 0; index < States.Length; index++) {
                if (States[index].ReferenceCount != 0) { throw new InvalidOperationException("Pending module acquisitions must be cancelled before disposing their manager."); }
            }
            List<int> order = new List<int>();
            int[] marks = new int[States.Length];
            try {
                for (int index = 0; index < States.Length; index++) { VisitDependency(index, marks, order); }
                for (int index = order.Count - 1; index >= 0; index--) { UnloadModule(order[index]); }
            } finally {
                NativeOwnership.Delete(order);
                NativeOwnership.Delete(marks);
            }
            for (int index = 0; index < States.Length; index++) {
                States[index].Dispose();
                NativeOwnership.Delete(States[index]);
            }
            NativeOwnership.Release(ref States);
            NativeOwnership.Release(ref SceneLeases);
            IsDisposed = true;
        }
    }
}
