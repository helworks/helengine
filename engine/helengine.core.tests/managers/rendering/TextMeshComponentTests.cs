using helengine;
using helengine.core.tests.scene.runtime;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Checks that reusable 3D text borrows own-font assets while owning and releasing only generated models.</summary>
    public sealed class TextMeshComponentTests {
        /// <summary>Uploads only after content changes; transforms and unchanged assignments reuse the current model.</summary>
        [Fact]
        public void DirtyGeometryReusesAndReplacesItsOwnedModel() {
            using TextMeshTestRenderManager renderer = new TextMeshTestRenderManager();
            using Core core = CreateCore(renderer);
            using FontAsset font = TextMeshGeometryTests.CreateFont();
            using RuntimeMaterial material = CreateMaterial(font);
            using Entity entity = CreateEntity(core);
            TextMeshComponent text = new TextMeshComponent { Font = font, Text = "A", Materials = new[] { material } }; entity.AddComponent(text);
            RuntimeModel first = text.Model;
            text.Text = "A"; text.Font = font; entity.Position = new float3(10, 20, 30);
            Assert.Same(first, text.Model); Assert.Equal(1, renderer.BuildCount);
            text.Text = "ó"; RuntimeModel second = text.Model;
            Assert.NotSame(first, second); Assert.Single(renderer.Released); Assert.Same(first, renderer.Released[0]);
            text.InvalidateGeometry(); Assert.NotSame(second, text.Model); Assert.Equal(3, renderer.BuildCount);
            text.Text = ""; Assert.Null(text.Model); Assert.Equal(3, renderer.BuildCount); Assert.Equal(3, renderer.Released.Count);
            text.Text = " \t\n"; Assert.Null(text.Model); Assert.Equal(3, renderer.BuildCount);
            Assert.False(font.IsDisposed); Assert.False(font.Texture.IsDisposed);
        }
        /// <summary>Registration follows enabled state, while detach/dispose release each model once and preserve borrowed assets.</summary>
        [Fact]
        public void DetachAndReattachUseTheirActualRenderers() {
            using TextMeshTestRenderManager firstRenderer = new TextMeshTestRenderManager();
            using TextMeshTestRenderManager secondRenderer = new TextMeshTestRenderManager();
            using Core firstCore = CreateCore(firstRenderer); using Core secondCore = CreateCore(secondRenderer);
            using FontAsset font = TextMeshGeometryTests.CreateFont(); using RuntimeMaterial material = CreateMaterial(font);
            using Entity first = CreateEntity(firstCore); using Entity second = CreateEntity(secondCore);
            TextMeshComponent text = new TextMeshComponent { Font = font, Text = "A", Materials = new[] { material } }; first.AddComponent(text);
            Assert.Contains(text, firstCore.ObjectManager.Drawables3D);
            first.Enabled = false; Assert.DoesNotContain(text, firstCore.ObjectManager.Drawables3D);
            first.Enabled = true; Assert.Contains(text, firstCore.ObjectManager.Drawables3D);
            text.RenderOrder3D = 3; Assert.Single(firstCore.ObjectManager.Drawables3D);
            SceneAssetOwnershipTestModel owned = (SceneAssetOwnershipTestModel)text.Model;
            first.RemoveComponent(text); Assert.Single(firstRenderer.Released); Assert.Equal(1, owned.DisposeCount);
            Assert.DoesNotContain(text, firstCore.ObjectManager.Drawables3D); Assert.Null(text.Model);
            second.AddComponent(text); Assert.NotSame(owned, text.Model); Assert.Equal(1, secondRenderer.BuildCount);
            second.RemoveComponent(text); text.Dispose(); text.Dispose(); Assert.Single(secondRenderer.Released);
            Assert.False(font.IsDisposed); Assert.Same(font.Texture, material.ResolvePrimaryTexture());
        }
        /// <summary>Invalid atlas bindings and failed uploads remain explicit and never destroy a previously valid model prematurely.</summary>
        [Fact]
        public void FailedReplacementRetainsOwnershipAndRejectsMissingGlyphs() {
            using TextMeshTestRenderManager renderer = new TextMeshTestRenderManager(); using Core core = CreateCore(renderer);
            using FontAsset font = TextMeshGeometryTests.CreateFont(); using RuntimeMaterial material = CreateMaterial(font);
            using Entity entity = CreateEntity(core);
            TextMeshComponent text = new TextMeshComponent { Font = font, Text = "A", Materials = new[] { material } }; entity.AddComponent(text);
            RuntimeModel first = text.Model; text.Text = "ó"; renderer.FailBuild = true;
            Assert.Throws<InvalidOperationException>(() => text.Model); Assert.Empty(renderer.Released);
            renderer.FailBuild = false; Assert.NotSame(first, text.Model); Assert.Single(renderer.Released);
            text.Text = "ç"; Assert.Throws<InvalidOperationException>(() => text.Model);
            text.Text = "A"; text.Materials = Array.Empty<RuntimeMaterial>(); Assert.Throws<InvalidOperationException>(() => text.Model);
            Assert.Throws<ArgumentNullException>(() => text.Text = null);
        }
        /// <summary>Computes a world plane through the same entity orientation used by ordinary 3D meshes.</summary>
        [Fact]
        public void PlaneNormalTracksTheEntityWithoutChangingGlyphGeometry() {
            using TextMeshTestRenderManager renderer = new TextMeshTestRenderManager(); using Core core = CreateCore(renderer);
            using Entity entity = CreateEntity(core); TextMeshComponent text = new TextMeshComponent(); entity.AddComponent(text);
            Assert.Equal(float3.UnitZ, text.WorldPlaneNormal);
            float4.CreateFromAxisAngle(float3.UnitY, Math.PI / 2, out float4 rotation); entity.Orientation = rotation;
            Assert.InRange(text.WorldPlaneNormal.X.ToDouble(), 0.999, 1.001);
            Assert.InRange(text.WorldPlaneNormal.Z.ToDouble(), -0.001, 0.001);
            Assert.Equal(0, renderer.BuildCount);
        }
        /// <summary>A disposed but still attached component cannot re-enter rendering when its owning entity is re-enabled.</summary>
        [Fact]
        public void AttachedDisposedTextStaysOutOfRenderQueues() {
            using TextMeshTestRenderManager renderer = new TextMeshTestRenderManager(); using Core core = CreateCore(renderer);
            using FontAsset font = TextMeshGeometryTests.CreateFont(); using RuntimeMaterial material = CreateMaterial(font);
            using Entity entity = CreateEntity(core);
            TextMeshComponent text = new TextMeshComponent { Font = font, Text = "A", Materials = new[] { material } }; entity.AddComponent(text);
            RuntimeModel model = text.Model; text.Dispose();
            Assert.DoesNotContain(text, core.ObjectManager.Drawables3D);
            entity.Enabled = false; entity.Enabled = true;
            Assert.DoesNotContain(text, core.ObjectManager.Drawables3D);
            Assert.Single(renderer.Released); Assert.Same(model, renderer.Released[0]);
            entity.RemoveComponent(text); text.Dispose(); Assert.Single(renderer.Released);
        }        /// <summary>Inherited nonuniform scale changes the real plane normal after a child rotation, beyond the decomposed orientation alone.</summary>
        [Fact]
        public void PlaneNormalUsesTheCompleteAffineHierarchy() {
            using TextMeshTestRenderManager renderer = new TextMeshTestRenderManager(); using Core core = CreateCore(renderer);
            using Entity root = CreateEntity(core); root.InitChildren(); root.Scale = new float3(2, 1, 1);
            Entity child = CreateEntity(core); root.AddChild(child);
            float4.CreateFromAxisAngle(float3.UnitY, Math.PI / 4, out float4 rotation); child.LocalOrientation = rotation;
            TextMeshComponent text = new TextMeshComponent(); child.AddComponent(text);
            Assert.InRange(text.WorldPlaneNormal.X.ToDouble(), 0.4471, 0.4473);
            Assert.InRange(text.WorldPlaneNormal.Z.ToDouble(), 0.8943, 0.8945);
            root.Scale = new float3(0, 1, 0); Assert.Equal(float3.Zero, text.WorldPlaneNormal);
        }        /// <summary>Creates an initialized owner with an explicit recording 3D renderer.</summary>
        static Core CreateCore(TextMeshTestRenderManager renderer) {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory) });
            core.Initialize(renderer, null, null, new PlatformInfo("test", "test")); return core;
        }
        /// <summary>Creates an entity with the component collection required by normal attach/detach lifecycle.</summary>
        static Entity CreateEntity(Core core) { Entity entity = new Entity(core); entity.InitComponents(); return entity; }
        /// <summary>Binds a borrowed atlas to an independently authored material, matching the component's explicit draw contract.</summary>
        static RuntimeMaterial CreateMaterial(FontAsset font) { RuntimeMaterial material = new RuntimeMaterial(); material.SetPrimaryTexture(font.Texture); return material; }
    }
}