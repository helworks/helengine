using helengine;

namespace helengine.core.tests {
    /// <summary>Verifies native hierarchy references retain detach-only public semantics and one final manual disposal owner.</summary>
    public sealed class EntityTeardownOwnershipTests {
        /// <summary>Detach preserves a component for reattachment, including insertion into a nonempty hierarchy container.</summary>
        [Fact]
        public void RemoveComponent_PreservesCallbacksAndAllowsReattachment() {
            Core core = CreateCore();
            Entity root = CreateEntity(core);
            EntityTeardownProbeComponent first = new EntityTeardownProbeComponent();
            EntityTeardownProbeComponent second = new EntityTeardownProbeComponent();
            root.AddComponent(first);
            root.AddComponent(second);
            root.RemoveComponent(first);
            Assert.Null(first.Parent);
            Assert.Equal(1, first.RemovedCount);
            Assert.Equal(0, first.DisposeCount);
            Assert.True(first.RemovedWhileAttached);
            root.AddComponent(first);
            Assert.Equal(2, first.AddedCount);
            root.Dispose();
            root.Dispose();
            Assert.Equal(1, first.DisposeCount);
            Assert.Equal(1, second.DisposeCount);
            Assert.Equal(2, first.RemovedCount);
            Assert.Equal(1, second.RemovedCount);
            Assert.True(first.RemovedWhileAttached && second.RemovedWhileAttached);
            core.Dispose();
        }

        /// <summary>A directly disposed child detaches from its parent without being deleted again by the parent's native list.</summary>
        [Fact]
        public void ChildDetachAndDispose_PreservesReparentingAndSiblingLifetime() {
            Core core = CreateCore();
            Entity parent = CreateEntity(core);
            Entity otherParent = CreateEntity(core);
            Entity child = CreateEntity(core);
            Entity sibling = CreateEntity(core);
            Entity grandchild = CreateEntity(core);
            EntityTeardownProbeComponent childProbe = new EntityTeardownProbeComponent();
            EntityTeardownProbeComponent grandchildProbe = new EntityTeardownProbeComponent();
            child.AddComponent(childProbe);
            grandchild.AddComponent(grandchildProbe);
            child.AddChild(grandchild);
            parent.AddChild(child);
            parent.AddChild(sibling);
            parent.RemoveChild(child);
            Assert.Null(child.Parent);
            Assert.False(child.IsDisposed);
            otherParent.AddChild(child);
            child.Dispose();
            Assert.Empty(otherParent.Children);
            Assert.True(child.IsDisposed && grandchild.IsDisposed);
            Assert.False(sibling.IsDisposed);
            Assert.Equal(1, childProbe.DisposeCount);
            Assert.Equal(1, grandchildProbe.DisposeCount);
            parent.Dispose();
            otherParent.Dispose();
            Assert.True(sibling.IsDisposed);
            Assert.Equal(1, childProbe.DisposeCount);
            Assert.Equal(1, grandchildProbe.DisposeCount);
            core.Dispose();
        }

        /// <summary>Two record indices preserve one owner across external untrack and ordinary scene unload.</summary>
        [Fact]
        public void SceneRecordIndices_UntrackDoesNotDisposeExternalRoots_AndUnloadDisposesOnce() {
            Core core = CreateCore();
            Entity externalRoot = CreateEntity(core);
            Entity unloadRoot = CreateEntity(core);
            EntityTeardownProbeComponent probe = new EntityTeardownProbeComponent();
            unloadRoot.AddComponent(probe);
            core.SceneManager.TrackExternallyLoadedScene("first", new List<Entity> { externalRoot }, false);
            core.SceneManager.TrackExternallyLoadedScene("second", new List<Entity> { unloadRoot }, false);
            Assert.True(core.SceneManager.TryUntrackExternallyLoadedScene("first"));
            Assert.False(core.SceneManager.TryUntrackExternallyLoadedScene("first"));
            Assert.False(externalRoot.IsDisposed);
            Assert.True(core.SceneManager.IsSceneLoaded("second"));
            core.SceneManager.UnloadScene("second");
            core.CompleteFrameBoundary();
            Assert.True(unloadRoot.IsDisposed);
            Assert.Equal(1, probe.DisposeCount);
            Assert.True(probe.RemovedWhileAttached);
            Assert.Empty(core.SceneManager.GetLoadedSceneIds());
            externalRoot.Dispose();
            core.Dispose();
        }

        /// <summary>Creates a real runtime core with scene lookup and no unrelated graphics or input dependencies.</summary>
        /// <returns>Initialized headless core for entity and scene-manager lifetime operations.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(new[] {
                    new RuntimeSceneCatalogEntry("first", "scenes/first.hasset"),
                    new RuntimeSceneCatalogEntry("second", "scenes/second.hasset")
                })
            });
            core.Initialize(new scene.runtime.SceneAssetOwnershipTestRenderManager(),
                new managers.rendering.Batch2DTestRenderManager(), null, new PlatformInfo("test", "test"));
            return core;
        }

        /// <summary>Creates hierarchy containers whose reference ownership is exercised by both public detachment and final disposal.</summary>
        /// <param name="core">Core that owns the entity's registrations.</param>
        /// <returns>Entity ready to receive components and children.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
