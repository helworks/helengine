using helengine;
using helengine.directx11;
namespace helengine.directx11.tests {
    /// <summary>Exercises backend binding reuse without constructing a DirectX device.</summary>
    public class DirectX11MaterialBindingCacheTests {
        /// <summary>Unchanged materials retain exactly the same prepared binding object between draws.</summary>
        [Fact]
        public void UnchangedMaterialReusesPreparedBindings() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var material = new ShaderRuntimeMaterial();
            Assert.Same(cache.Get(material), cache.Get(material));
        }
        /// <summary>Builds a valid sixteen-byte material layout with an optional texture binding.</summary>
        static ShaderRuntimeMaterial Material(byte value = 1) {
            var material = new ShaderRuntimeMaterial();
            material.SetLayout(new MaterialLayout("test", "vertex", "pixel", "default", new MaterialRenderState(),
                new[] { new MaterialLayoutBinding("Albedo", ShaderResourceType.Texture2D, 0, 32, 0) },
                new[] { new MaterialLayoutBinding("Color", ShaderResourceType.ConstantBuffer, 0, 4, 16) }, Array.Empty<MaterialLayoutBinding>()));
            var bytes = new byte[16]; bytes[0] = value;
            material.Properties.SetConstantBufferData(0, bytes);
            return material;
        }
        /// <summary>Equal content shares one resource across independent material identities and releases on the final owner.</summary>
        [Fact]
        public void EqualContentsShareResourceUntilLastRelease() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var first = Material(); var second = Material();
            var resource = cache.Get(first).Constants[0].Content.Resource;
            Assert.Same(resource, cache.Get(second).Constants[0].Content.Resource);
            Assert.Equal(1, cache.ContentCount);
            cache.Release(first); Assert.True(resource.CanRead);
            cache.Release(second); Assert.False(resource.CanRead);
            Assert.Equal(0, cache.ContentCount);
        }
        /// <summary>Parent changes and local clears rebuild inherited data without retaining obsolete resource versions.</summary>
        [Fact]
        public void ParentChangesAndClearedOverridesRefreshBindings() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var parent = Material(); var child = new ShaderRuntimeMaterial(); child.SetParentMaterial(parent);
            var old = cache.Get(child);
            parent.Properties.SetConstantBufferData(0, new byte[16]);
            var changed = cache.Get(child); Assert.NotSame(old, changed); Assert.Equal(0, changed.Constants[0].Content.Data[0]);
            var bytes = new byte[16]; bytes[0] = 7;
            child.Properties.SetConstantBufferData(0, bytes); Assert.Equal(7, cache.Get(child).Constants[0].Content.Data[0]);
            child.Properties.SetConstantBufferData(0, null); Assert.Equal(0, cache.Get(child).Constants[0].Content.Data[0]);
            Assert.Equal(1, cache.ContentCount);
            parent.Properties.SetConstantBufferData(0, null); Assert.Null(cache.Get(child).Constants[0].Content);
            Assert.Equal(0, cache.ContentCount);
        }
        /// <summary>Direct binding-array edits bypass layout revisions and must still invalidate prepared slots.</summary>
        [Fact]
        public void InPlaceBindingReplacementInvalidatesSlot() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var material = Material(); var old = cache.Get(material); var revision = material.LayoutRevision;
            material.Layout.ConstantBufferBindings[0] = new MaterialLayoutBinding("Color", ShaderResourceType.ConstantBuffer, 0, 7, 16);
            Assert.Equal(revision, material.LayoutRevision);
            Assert.NotSame(old, cache.Get(material)); Assert.Equal(7, cache.Get(material).Constants[0].Slot);
        }
        /// <summary>Ancestor release invalidates cached descendants even if the ancestor was never itself drawn.</summary>
        [Fact]
        public void ParentReleaseDropsAllDependentResources() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var parent = Material(); var child = new ShaderRuntimeMaterial(); child.SetParentMaterial(parent);
            var resource = cache.Get(child).Constants[0].Content.Resource;
            cache.Release(parent); Assert.Equal(0, cache.MaterialCount); Assert.Equal(0, cache.ContentCount); Assert.False(resource.CanRead);
        }
        /// <summary>Renderer-owned dynamic buffers never enter the material content pool.</summary>
        [Theory]
        [InlineData("TransformBuffer")][InlineData("ForwardLightBuffer")][InlineData("ShadowBuffer")]
        public void EngineConstantsRemainDynamic(string name) {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var material = Material();
            material.Layout.ConstantBufferBindings[0] = new MaterialLayoutBinding(name, ShaderResourceType.ConstantBuffer, 0, 0, 16);
            Assert.Empty(cache.Get(material).Constants); Assert.Equal(0, cache.ContentCount);
        }
        /// <summary>Reparenting updates dependency identity even when both parents have matching revisions.</summary>
        [Fact]
        public void ReparentingReplacesInheritedContents() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var first = Material(1); var second = Material(2); var child = new ShaderRuntimeMaterial();
            child.SetParentMaterial(first); var old = cache.Get(child);
            child.SetParentMaterial(second); Assert.NotSame(old, cache.Get(child)); Assert.Equal(2, cache.Get(child).Constants[0].Content.Data[0]);
            cache.Release(first); Assert.Equal(1, cache.MaterialCount);
        }
        /// <summary>Factory failures publish no partially prepared entry and release resources acquired earlier in the same preparation.</summary>
        [Fact]
        public void FailedPreparationRollsBackAcquiredResources() {
            MemoryStream created = null;
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => data[0] == 2 ? throw new InvalidOperationException("upload") : created = new MemoryStream(data));
            var material = new ShaderRuntimeMaterial();
            material.SetLayout(new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), Array.Empty<MaterialLayoutBinding>(),
                new[] { new MaterialLayoutBinding("A", ShaderResourceType.ConstantBuffer, 0, 4, 16), new MaterialLayoutBinding("B", ShaderResourceType.ConstantBuffer, 0, 5, 16) }, Array.Empty<MaterialLayoutBinding>()));
            var bytes = new byte[16]; bytes[0] = 1; material.Properties.SetConstantBufferData(0, bytes);
            bytes[0] = 2; material.Properties.SetConstantBufferData(1, bytes);
            Assert.Throws<InvalidOperationException>(() => cache.Get(material));
            Assert.NotNull(created); Assert.False(created.CanRead); Assert.Equal(0, cache.ContentCount); Assert.Equal(0, cache.MaterialCount);
        }
        /// <summary>Full disposal closes shared resources and drops all material and content references.</summary>
        [Fact]
        public void DisposeReleasesSharedResources() {
            var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var resource = cache.Get(Material()).Constants[0].Content.Resource;
            cache.Dispose(); Assert.False(resource.CanRead); Assert.Equal(0, cache.MaterialCount); Assert.Equal(0, cache.ContentCount);
        }
        /// <summary>Texture-only mutations retain unchanged constant resources while updating inherited texture identity.</summary>
        [Fact]
        public void TextureMutationReusesConstantResource() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var material = Material(); var first = new DirectX11TextureResource(); var second = new DirectX11TextureResource();
            material.Properties.SetTexture(0, first);
            var prepared = cache.Get(material); var resource = prepared.Constants[0].Content.Resource;
            material.Properties.SetTexture(0, second);
            Assert.Same(second, cache.Get(material).Textures[0].Texture);
            Assert.Same(resource, cache.Get(material).Constants[0].Content.Resource);
        }
        /// <summary>Parent texture overrides and null clears preserve the runtime inheritance contract.</summary>
        [Fact]
        public void TextureOverrideClearAndLayoutMutationRefreshBindings() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var parent = Material(); var child = new ShaderRuntimeMaterial(); child.SetParentMaterial(parent);
            var first = new DirectX11TextureResource(); var second = new DirectX11TextureResource();
            parent.Properties.SetTexture(0, first); Assert.Same(first, cache.Get(child).Textures[0].Texture);
            child.Properties.SetTexture(0, second); Assert.Same(second, cache.Get(child).Textures[0].Texture);
            child.Properties.SetTexture(0, null); Assert.Same(first, cache.Get(child).Textures[0].Texture);
            parent.Layout.TextureBindings[0] = new MaterialLayoutBinding("Albedo", ShaderResourceType.Texture2D, 0, 35, 0);
            Assert.Equal(35, cache.Get(child).Textures[0].Binding.Slot);
            parent.Properties.SetTexture(0, null); Assert.Empty(cache.Get(child).Textures);
        }
        /// <summary>Repeated cache hits allocate no new managed binding objects or copied constant payloads.</summary>
        [Fact]
        public void WarmCacheHitsDoNotAllocate() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var material = Material(); cache.Get(material);
            for (int i = 0; i < 21000; i++) cache.Get(material);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 21000; i++) cache.Get(material);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        /// <summary>Large material populations with identical constants retain one shared upload resource.</summary>
        [Fact]
        public void ManyIdenticalMaterialsShareOneResource() {
            int creations = 0;
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => { creations++; return new MemoryStream(data); });
            var parent = Material();
            for (int i = 0; i < 21000; i++) {
                var child = new ShaderRuntimeMaterial(); child.SetParentMaterial(parent); cache.Get(child);
            }
            Assert.Equal(21000, cache.MaterialCount); Assert.Equal(1, creations); Assert.Equal(1, cache.ContentCount);
            cache.Release(parent); Assert.Equal(0, cache.ContentCount); Assert.Equal(0, cache.MaterialCount);
        }
        /// <summary>Rebuilding a parent retains dormant descendants until their dependency revisions are checked on use.</summary>
        [Fact]
        public void ParentRebuildRefreshesOnlyRequestedEntry() {
            using var cache = new DirectX11MaterialBindingCache<MemoryStream>(data => new MemoryStream(data));
            var parent = Material();
            var child = new ShaderRuntimeMaterial();
            child.SetParentMaterial(parent);
            cache.Get(parent);
            var originalChild = cache.Get(child);
            var originalResource = originalChild.Constants[0].Content.Resource;
            parent.Properties.SetConstantBufferData(0, new byte[16]);
            var rebuiltParent = cache.Get(parent);
            Assert.Equal(2, cache.MaterialCount);
            Assert.True(originalResource.CanRead);
            Assert.False(originalChild.IsCurrent());
            var rebuiltChild = cache.Get(child);
            Assert.NotSame(originalChild, rebuiltChild);
            Assert.Same(rebuiltParent.Constants[0].Content, rebuiltChild.Constants[0].Content);
            Assert.Equal(0, rebuiltChild.Constants[0].Content.Data[0]);
            Assert.False(originalResource.CanRead);
            Assert.Equal(1, cache.ContentCount);
            cache.Release(parent);
            Assert.Equal(0, cache.MaterialCount);
            Assert.Equal(0, cache.ContentCount);
        }
    }
}
