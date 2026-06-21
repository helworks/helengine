using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Locks shared runtime ownership seams that managed tests cannot observe directly in native builds.
    /// </summary>
    public sealed class RuntimeOwnershipSourceAuditTests {
        /// <summary>
        /// Ensures components expose a disposal seam and entity teardown deletes removed children and components on the native side.
        /// </summary>
        [Fact]
        public void Dispose_whenEntityHierarchyIsTornDown_releasesComponentAndChildInstancesThroughNativeOwnership() {
            string componentSource = ReadSource("helengine.core", "Component.cs");
            string entitySource = ReadSource("helengine.core", "Entity.cs");

            Assert.Contains("public virtual void Dispose()", componentSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(child);", entitySource);
            Assert.Contains("component.Dispose();", entitySource);
            Assert.Contains("NativeOwnership.Delete(component);", entitySource);
            Assert.Contains("List<Component> components = Components;", entitySource);
            Assert.Contains("NativeOwnership.Delete(components);", entitySource);
            Assert.Contains("List<Entity> children = Children;", entitySource);
            Assert.Contains("NativeOwnership.Delete(children);", entitySource);
        }

        /// <summary>
        /// Ensures scene-manager root teardown deletes disposed root entities instead of only calling their dispose lifecycle.
        /// </summary>
        [Fact]
        public void Dispose_whenSceneManagerTearsDownSceneRoots_deletesDisposedRootEntities() {
            string sceneManagerSource = ReadSource("helengine.core", "scene", "runtime", "SceneManager.cs");

            Assert.Contains("NativeOwnership.DisposeAndDelete(rootEntities[index]);", sceneManagerSource);
            Assert.Contains("NativeOwnership.Delete(sceneLoadingEventArgs);", sceneManagerSource);
            Assert.Contains("NativeOwnership.Delete(sceneLoadedEventArgs);", sceneManagerSource);
            Assert.Contains("NativeOwnership.Delete(sceneUnloadingEventArgs);", sceneManagerSource);
            Assert.Contains("NativeOwnership.Delete(sceneUnloadedEventArgs);", sceneManagerSource);
        }

        /// <summary>
        /// Ensures scene-load timing diagnostics release temporary stopwatch instances after native scene materialization logging completes.
        /// </summary>
        [Fact]
        public void Load_whenRuntimeSceneMaterializationLogsDuration_deletesTemporaryStopwatch() {
            string runtimeSceneLoadServiceSource = ReadSource("helengine.core", "scene", "runtime", "RuntimeSceneLoadService.cs");

            Assert.Contains("System.Diagnostics.Stopwatch loadStopwatch = System.Diagnostics.Stopwatch.StartNew();", runtimeSceneLoadServiceSource);
            Assert.Contains("NativeOwnership.Delete(loadStopwatch);", runtimeSceneLoadServiceSource);
        }

        /// <summary>
        /// Ensures binary string decoding releases the temporary byte buffer after native UTF-8 conversion.
        /// </summary>
        [Fact]
        public void ReadString_whenBinaryReaderDecodesUtf8_releasesTemporaryByteBuffer() {
            string engineBinaryReaderSource = ReadSource("helengine.core", "serialization", "EngineBinaryReader.cs");

            Assert.Contains("byte[] bytes = ReadBytes(length);", engineBinaryReaderSource);
            Assert.Contains("NativeOwnership.Delete(bytes);", engineBinaryReaderSource);
        }

        /// <summary>
        /// Ensures menu runtime teardown releases bound panel and item runtimes together with temporary binding lists used during initialization.
        /// </summary>
        [Fact]
        public void Dispose_whenMenuRuntimeIsRemoved_releasesPanelStateAndTemporaryBindingLists() {
            string menuComponentSource = ReadSource("helengine.core", "components", "2d", "menu", "MenuComponent.cs");

            Assert.Contains("public override void Dispose()", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(PanelsById);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(PanelRuntimes);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(PanelHistory);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(panelEntities);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(itemEntities);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(markerEntities);", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(scrollEntities);", menuComponentSource);
            Assert.Contains("panelRuntime.ItemsScrollComponent.ScrollOffsetChanged -= HandleItemsScrollOffsetChanged;", menuComponentSource);
            Assert.Contains("NativeOwnership.Delete(panelRuntime.Items);", menuComponentSource);
        }

        /// <summary>
        /// Ensures camera teardown releases per-camera render queues and renderer intent objects created for native runtime cameras.
        /// </summary>
        [Fact]
        public void Dispose_whenCameraComponentIsRemoved_releasesRenderQueuesAndRenderSettings() {
            string cameraComponentSource = ReadSource("helengine.core", "components", "CameraComponent.cs");
            string renderList2DSource = ReadSource("helengine.core", "managers", "rendering", "RenderList2D.cs");
            string renderList3DSource = ReadSource("helengine.core", "managers", "rendering", "RenderList3D.cs");

            Assert.Contains("public override void ComponentRemoved(Entity entity)", cameraComponentSource);
            Assert.Contains("Core.Instance.ObjectManager.RemoveCamera(this);", cameraComponentSource);
            Assert.Contains("public override void Dispose()", cameraComponentSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(renderList2D);", cameraComponentSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(renderList3D);", cameraComponentSource);
            Assert.Contains("NativeOwnership.Delete(RenderSettingsValue);", cameraComponentSource);
            Assert.Contains("public sealed class RenderList2D : IRenderQueue2D, IDisposable", renderList2DSource);
            Assert.Contains("NativeOwnership.Delete(Items);", renderList2DSource);
            Assert.Contains("public sealed class RenderList3D : IRenderQueue3D, IDisposable", renderList3DSource);
            Assert.Contains("NativeOwnership.Delete(Items);", renderList3DSource);
        }

        /// <summary>
        /// Ensures viewport scaling components delete native snapshot records instead of only clearing the managed list wrapper.
        /// </summary>
        [Fact]
        public void Dispose_whenViewportScalingComponentsAreRemoved_releasesSnapshotListsAndItems() {
            string viewportComponentSource = ReadSource("helengine.core", "components", "ViewportComponent.cs");
            string referenceCanvasFitComponentSource = ReadSource("helengine.core", "components", "ReferenceCanvasFitComponent.cs");

            Assert.Contains("public override void Dispose()", viewportComponentSource);
            Assert.Contains("ReleaseLayoutSnapshots();", viewportComponentSource);
            Assert.Contains("NativeOwnership.Delete(LayoutSnapshotsValue[snapshotIndex]);", viewportComponentSource);
            Assert.Contains("NativeOwnership.Delete(LayoutSnapshotsValue);", viewportComponentSource);

            Assert.Contains("public override void Dispose()", referenceCanvasFitComponentSource);
            Assert.Contains("ReleaseSnapshots();", referenceCanvasFitComponentSource);
            Assert.Contains("NativeOwnership.Delete(SnapshotsValue[snapshotIndex]);", referenceCanvasFitComponentSource);
            Assert.Contains("NativeOwnership.Delete(SnapshotsValue);", referenceCanvasFitComponentSource);
        }

        /// <summary>
        /// Ensures drawable components explicitly unregister from render lists and release native array wrappers they own.
        /// </summary>
        [Fact]
        public void Dispose_whenDrawableComponentsAreRemoved_unregistersRenderEntriesAndOwnedArrays() {
            string meshComponentSource = ReadSource("helengine.core", "components", "3d", "MeshComponent.cs");
            string textComponentSource = ReadSource("helengine.core", "components", "2d", "TextComponent.cs");
            string spriteComponentSource = ReadSource("helengine.core", "components", "2d", "SpriteComponent.cs");
            string roundedRectComponentSource = ReadSource("helengine.core", "components", "2d", "RoundedRectComponent.cs");

            Assert.Contains("public override void ComponentRemoved(Entity entity)", meshComponentSource);
            Assert.Contains("Core.Instance.ObjectManager.RemoveFromRender3D(this);", meshComponentSource);
            Assert.Contains("public override void Dispose()", meshComponentSource);
            Assert.Contains("NativeOwnership.Release(ref MaterialsBySlot);", meshComponentSource);

            Assert.Contains("public override void ComponentRemoved(Entity entity)", textComponentSource);
            Assert.Contains("Core.Instance.ObjectManager.RemoveFromRender2D(this);", textComponentSource);

            Assert.Contains("public override void ComponentRemoved(Entity entity)", spriteComponentSource);
            Assert.Contains("Core.Instance.ObjectManager.RemoveFromRender2D(this);", spriteComponentSource);

            Assert.Contains("public override void ComponentRemoved(Entity entity)", roundedRectComponentSource);
            Assert.Contains("Core.Instance.ObjectManager.RemoveFromRender2D(this);", roundedRectComponentSource);
        }

        /// <summary>
        /// Ensures runtime-created FPS overlay entities are deleted when the owning component tears down its generated subtree.
        /// </summary>
        [Fact]
        public void Dispose_whenFpsOverlayIsTornDown_deletesGeneratedOverlayEntitySubtree() {
            string fpsComponentSource = ReadSource("helengine.core", "components", "2d", "FPSComponent.cs");

            Assert.Contains("NativeOwnership.DisposeAndDelete(overlayHost);", fpsComponentSource);
            Assert.DoesNotContain("overlayHost.Dispose();", fpsComponentSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures menu-driven scene transitions resolve logical ids through the optional scene-map singleton helper instead of the removed scene-map service seam.
        /// </summary>
        [Fact]
        public void Load_whenMenuTransitionsScenes_usesSceneMapComponentHelperAndDoesNotReferenceSceneMapService() {
            string coreSource = ReadSource("helengine.core", "Core.cs");
            string menuComponentSource = ReadSource("helengine.core", "components", "2d", "menu", "MenuComponent.cs");
            string returnToMenuSource = ReadSource("helengine.core", "components", "2d", "menu", "DemoDiscReturnToMenuRuntimeComponent.cs");

            Assert.DoesNotContain("SceneMapService", coreSource, StringComparison.Ordinal);
            Assert.Contains("string resolvedSceneId = SceneMapComponent.ResolveSceneId(sceneId);", menuComponentSource);
            Assert.Contains("Core.Instance.SceneManager.LoadScene(resolvedSceneId, SceneLoadMode.Single);", menuComponentSource);
            Assert.DoesNotContain("Core.Instance.SceneManager.LoadScene(sceneId, SceneLoadMode.Single);", menuComponentSource, StringComparison.Ordinal);
            Assert.Contains("string resolvedSceneId = SceneMapComponent.ResolveSceneId(MainMenuSceneId);", returnToMenuSource);
            Assert.DoesNotContain("Core.Instance.SceneMapService", returnToMenuSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures runtime material, layout, property-block, and model ownership seams release nested native containers instead of only deleting the top-level wrapper objects.
        /// </summary>
        [Fact]
        public void Dispose_whenRuntimeRenderingStateIsReleased_deletesNestedNativeContainersAndPreviousReplacements() {
            string runtimeMaterialSource = ReadSource("helengine.core", "assets", "RuntimeMaterial.cs");
            string runtimeModelSource = ReadSource("helengine.core", "assets", "RuntimeModel.cs");
            string materialLayoutSource = ReadSource("helengine.core", "assets", "material", "MaterialLayout.cs");
            string materialPropertyBlockSource = ReadSource("helengine.core", "assets", "material", "MaterialPropertyBlock.cs");

            Assert.Contains("public class RuntimeMaterial : RuntimeData, IDisposable", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.Delete(previousRenderState);", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(previousProperties);", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(LayoutValue);", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.Delete(RenderStateValue);", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.DisposeAndDelete(PropertiesValue);", runtimeMaterialSource);
            Assert.Contains("NativeOwnership.Delete(ChildMaterialsValue);", runtimeMaterialSource);

            Assert.Contains("public abstract class RuntimeModel : RuntimeData, IDisposable", runtimeModelSource);
            Assert.Contains("NativeOwnership.DeleteItemsAndRelease(ref SubmeshesValue);", runtimeModelSource);

            Assert.Contains("public class MaterialLayout : IDisposable", materialLayoutSource);
            Assert.Contains("NativeOwnership.Delete(RenderStateValue);", materialLayoutSource);
            Assert.Contains("ReleaseBindings(ref TextureBindingsValue);", materialLayoutSource);
            Assert.Contains("ReleaseBindings(ref ConstantBufferBindingsValue);", materialLayoutSource);
            Assert.Contains("ReleaseBindings(ref SamplerBindingsValue);", materialLayoutSource);
            Assert.Contains("static void ReleaseBindings(ref MaterialLayoutBinding[] bindings)", materialLayoutSource);

            Assert.Contains("public class MaterialPropertyBlock : IDisposable", materialPropertyBlockSource);
            Assert.Contains("NativeOwnership.DeleteItemsAndRelease(ref ConstantBufferValues);", materialPropertyBlockSource);
            Assert.Contains("NativeOwnership.Release(ref TextureValues);", materialPropertyBlockSource);
        }

        /// <summary>
        /// Reads one source file from the engine tree used by native ownership audits.
        /// </summary>
        /// <param name="segments">Relative path segments under the engine folder.</param>
        /// <returns>Full source text.</returns>
        static string ReadSource(params string[] segments) {
            string[] fullSegments = new string[segments.Length + 5];
            fullSegments[0] = AppContext.BaseDirectory;
            fullSegments[1] = "..";
            fullSegments[2] = "..";
            fullSegments[3] = "..";
            fullSegments[4] = "..";
            for (int index = 0; index < segments.Length; index++) {
                fullSegments[index + 5] = segments[index];
            }

            string sourcePath = Path.GetFullPath(Path.Combine(fullSegments));
            return File.ReadAllText(sourcePath);
        }
    }
}
