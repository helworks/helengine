using helengine;
namespace helengine.directx11.tests {
    /// <summary>Checks controlled binding mutations and constructor alias isolation.</summary>
    public class MaterialLayoutBindingsTests {
        /// <summary>Constructor arrays cannot silently alter material binding slots.</summary>
        [Fact]
        public void ConstructorCopiesBindingSlots() {
            var original = new MaterialLayoutBinding("A", ShaderResourceType.Texture2D, 0, 1, 0);
            var source = new[] { original };
            using var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), source, Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            source[0] = null;
            Assert.Same(original, layout.TextureBindings[0]);
        }
        /// <summary>All binding categories expose a revision for constant-time validation.</summary>
        [Fact]
        public void BindingCollectionsExposeRevisions() {
            using var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            Assert.NotNull(layout.TextureBindings.GetType().GetProperty("Revision"));
            Assert.NotNull(layout.ConstantBufferBindings.GetType().GetProperty("Revision"));
            Assert.NotNull(layout.SamplerBindings.GetType().GetProperty("Revision"));
        }
        /// <summary>Obtains the public revision without requiring the not-yet-implemented collection type.</summary>
        static ulong Revision(object bindings) {
            var property = bindings.GetType().GetProperty("Revision");
            Assert.NotNull(property);
            return Assert.IsType<ulong>(property.GetValue(bindings));
        }
        /// <summary>Replacing, clearing and reordering slots advances the revision, while identical writes do not.</summary>
        [Fact]
        public void EffectiveWritesAdvanceRevision() {
            var a = new MaterialLayoutBinding("A", ShaderResourceType.Texture2D, 0, 1, 0);
            var b = new MaterialLayoutBinding("B", ShaderResourceType.Texture2D, 0, 2, 0);
            using var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), new[] { a, b }, Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            ulong revision = Revision(layout.TextureBindings);
            layout.TextureBindings[0] = a;
            Assert.Equal(revision, Revision(layout.TextureBindings));
            layout.TextureBindings[0] = b;
            layout.TextureBindings[1] = a;
            Assert.Equal(revision + 2, Revision(layout.TextureBindings));
            Assert.Same(b, layout.TextureBindings[0]);
            Assert.Same(a, layout.TextureBindings[1]);
            layout.TextureBindings[0] = null;
            Assert.Equal(revision + 3, Revision(layout.TextureBindings));
            layout.TextureBindings[0] = null;
            Assert.Equal(revision + 3, Revision(layout.TextureBindings));
        }
        /// <summary>Rejected writes leave both revision and slot contents unchanged.</summary>
        [Fact]
        public void InvalidIndicesDoNotMutate() {
            using var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), new MaterialLayoutBinding[1], Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            ulong revision = Revision(layout.TextureBindings);
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TextureBindings[-1] = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.TextureBindings[1] = null);
            Assert.Equal(revision, Revision(layout.TextureBindings));
            Assert.Null(layout.TextureBindings[0]);
        }
        /// <summary>Revision exhaustion cannot wrap and make an old cache entry current again.</summary>
        [Fact]
        public void RevisionOverflowRejectsMutation() {
            using var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), new MaterialLayoutBinding[1], Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            var field = layout.TextureBindings.GetType().GetField("RevisionValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(layout.TextureBindings, ulong.MaxValue);
            Assert.Throws<InvalidOperationException>(() => layout.TextureBindings[0] = new MaterialLayoutBinding("A", ShaderResourceType.Texture2D, 0, 1, 0));
            Assert.Null(layout.TextureBindings[0]);
            Assert.Equal(ulong.MaxValue, Revision(layout.TextureBindings));
        }
        /// <summary>Disposal is repeatable and previously retained collections cannot be read or mutated afterward.</summary>
        [Fact]
        public void DisposalInvalidatesRetainedCollection() {
            var layout = new MaterialLayout("test", "v", "p", "default", new MaterialRenderState(), new MaterialLayoutBinding[1], Array.Empty<MaterialLayoutBinding>(), Array.Empty<MaterialLayoutBinding>());
            var bindings = layout.TextureBindings;
            layout.Dispose();
            layout.Dispose();
            Assert.Null(layout.TextureBindings);
            Assert.Throws<InvalidOperationException>(() => bindings[0]);
            Assert.Throws<InvalidOperationException>(() => bindings[0] = null);
        }
        /// <summary>Public disposal cannot destroy the shared empty collections used by unconfigured materials.</summary>
        [Fact]
        public void SharedEmptyCollectionsSurviveExplicitDisposal() {
            var layout = MaterialLayout.Empty;
            var textures = layout.TextureBindings;
            var constants = layout.ConstantBufferBindings;
            var samplers = layout.SamplerBindings;
            ulong textureRevision = textures.Revision;
            ulong constantRevision = constants.Revision;
            ulong samplerRevision = samplers.Revision;
            textures.Dispose();
            constants.Dispose();
            samplers.Dispose();
            layout.Dispose();
            Assert.Equal(0, textures.Length);
            Assert.Equal(0, constants.Length);
            Assert.Equal(0, samplers.Length);
            Assert.Equal(textureRevision, textures.Revision);
            Assert.Equal(constantRevision, constants.Revision);
            Assert.Equal(samplerRevision, samplers.Revision);
            Assert.Equal(-1, layout.FindTextureBindingIndex("missing"));
            Assert.Equal(-1, layout.FindConstantBufferBindingIndex("missing"));
            Assert.Equal(-1, layout.FindSamplerBindingIndex("missing"));
        }
    }
}
