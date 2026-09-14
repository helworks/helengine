using helengine;
using Xunit;

namespace helengine.core.tests.managers {
    /// <summary>
    /// Verifies the cached update-loop crash breadcrumbs stay identical to the values the previous per-frame reflection and component scans produced.
    /// </summary>
    public sealed class ObjectManagerUpdateDiagnosticsTests {
        [Fact]
        public void Update_records_type_hash_and_owner_scene_entity_id_matching_the_uncached_computation() {
            Core core = CreateInitializedCore();
            Entity entity = CreateEntityWithSceneEntityId(core, 4242u, out DiagnosticProbeUpdateComponent probe);

            core.ObjectManager.Update();

            Assert.True(probe.UpdateCount > 0);
            Assert.Equal(ComputeExpectedTypeNameHash(probe), core.ObjectManager.LastUpdateableDiagnosticTypeHash);
            Assert.Equal(ResolveExpectedOwnerSceneEntityId(entity), core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
            Assert.Equal(4242u, core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
        }

        [Fact]
        public void Update_records_zero_owner_scene_entity_id_when_no_scene_entity_id_component_is_attached() {
            Core core = CreateInitializedCore();
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            DiagnosticProbeUpdateComponent probe = new DiagnosticProbeUpdateComponent();
            entity.AddComponent(probe);
            entity.InitializeHierarchy();

            core.ObjectManager.Update();

            Assert.Equal(ResolveExpectedOwnerSceneEntityId(entity), core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
            Assert.Equal(0u, core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
        }

        [Fact]
        public void Update_records_the_remaining_scene_entity_id_after_the_first_metadata_component_is_removed() {
            Core core = CreateInitializedCore();
            Entity entity = CreateEntityWithSceneEntityId(core, 11u, out DiagnosticProbeUpdateComponent probe);
            SceneEntityRuntimeIdComponent second = new SceneEntityRuntimeIdComponent();
            second.SceneEntityId = 22u;
            entity.AddComponent(second);

            core.ObjectManager.Update();
            Assert.Equal(11u, core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);

            entity.RemoveComponent(entity.Components[0]);
            core.ObjectManager.Update();

            Assert.Equal(ResolveExpectedOwnerSceneEntityId(entity), core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
            Assert.Equal(22u, core.ObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
        }

        [Fact]
        public void Update_records_distinct_cached_hashes_for_distinct_updateable_types() {
            Core core = CreateInitializedCore();
            Entity first = CreateEntityWithSceneEntityId(core, 1u, out DiagnosticProbeUpdateComponent probe);
            Entity second = new Entity(core);
            second.InitComponents();
            second.InitChildren();
            SecondDiagnosticProbeUpdateComponent otherProbe = new SecondDiagnosticProbeUpdateComponent();
            second.AddComponent(otherProbe);
            second.InitializeHierarchy();

            core.ObjectManager.Update();

            uint firstHash = ComputeExpectedTypeNameHash(probe);
            uint secondHash = ComputeExpectedTypeNameHash(otherProbe);
            Assert.NotEqual(firstHash, secondHash);
            Assert.Equal(secondHash, core.ObjectManager.LastUpdateableDiagnosticTypeHash);
            Assert.Equal(2, core.ObjectManager.Updateables.Count);
            Assert.NotNull(first);
        }

        /// <summary>
        /// Creates one headless core whose object manager is materialized so entities can register against it.
        /// </summary>
        /// <returns>Initialized core instance.</returns>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        static Entity CreateEntityWithSceneEntityId(Core core, uint sceneEntityId, out DiagnosticProbeUpdateComponent probe) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            SceneEntityRuntimeIdComponent runtimeId = new SceneEntityRuntimeIdComponent();
            runtimeId.SceneEntityId = sceneEntityId;
            entity.AddComponent(runtimeId);
            probe = new DiagnosticProbeUpdateComponent();
            entity.AddComponent(probe);
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Reproduces the pre-cache hash computation: FNV-1a over the concrete runtime type name.
        /// </summary>
        /// <param name="item">Updateable whose type name should be hashed.</param>
        /// <returns>Stable non-cryptographic hash of the runtime type name.</returns>
        static uint ComputeExpectedTypeNameHash(IUpdateable item) {
            string typeName = item.GetType().Name;
            uint hash = 2166136261u;
            for (int index = 0; index < typeName.Length; index++) {
                hash ^= typeName[index];
                hash *= 16777619u;
            }

            return hash;
        }

        /// <summary>
        /// Reproduces the pre-cache owner lookup: a linear scan for the first scene-entity id metadata component.
        /// </summary>
        /// <param name="entity">Owner entity to scan.</param>
        /// <returns>Authored scene entity id, or <c>0</c> when unavailable.</returns>
        static uint ResolveExpectedOwnerSceneEntityId(Entity entity) {
            if (entity == null || entity.Components == null) {
                return 0u;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is SceneEntityRuntimeIdComponent runtimeIdComponent) {
                    return runtimeIdComponent.SceneEntityId;
                }
            }

            return 0u;
        }

        /// <summary>
        /// Minimal updateable component used to drive one object-manager update pass.
        /// </summary>
        sealed class DiagnosticProbeUpdateComponent : UpdateComponent {
            /// <summary>
            /// Gets the number of update passes this probe executed.
            /// </summary>
            public int UpdateCount { get; private set; }

            /// <summary>
            /// Counts one executed update pass.
            /// </summary>
            public override void Update() {
                UpdateCount++;
            }
        }

        /// <summary>
        /// Second updateable component type used to prove the per-type hash cache keeps distinct types distinct.
        /// </summary>
        sealed class SecondDiagnosticProbeUpdateComponent : UpdateComponent {
            /// <summary>
            /// Performs no work; the type only exists to exercise a second cache entry.
            /// </summary>
            public override void Update() {
            }
        }
    }
}
