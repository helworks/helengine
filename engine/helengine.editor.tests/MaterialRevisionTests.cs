using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Protects the observable invalidation contract used by renderer-owned immutable property copies.</summary>
    public class MaterialRevisionTests {
        /// <summary>Every successful write/clear invalidates, while owned reads and rejected payloads preserve the current revision.</summary>
        [Fact]
        public void Properties_TrackAllWritesWithoutChangingCopyIsolation() {
            using var block = new MaterialPropertyBlock(CreateLayout());
            ulong revision = block.Revision;
            block.SetTexture("Texture", new TestRuntimeTexture());
            Assert.True(block.Revision > revision); revision = block.Revision;
            block.SetTexture(0, null);
            Assert.True(block.Revision > revision); revision = block.Revision;
            block.SetConstantBufferData("Params", new byte[] { 1, 2, 3, 4 });
            Assert.True(block.Revision > revision); revision = block.Revision;
            byte[] copy = block.GetConstantBufferData(0); copy[0] = 99;
            Assert.Equal(revision, block.Revision);
            Assert.Equal((byte)1, block.GetConstantBufferData(0)[0]);
            Assert.Throws<InvalidOperationException>(() => block.SetConstantBufferData(0, new byte[] { 1 }));
            Assert.Equal(revision, block.Revision);
            block.SetConstantBufferData(0, null);
            Assert.True(block.Revision > revision); revision = block.Revision;
            block.Dispose();
            Assert.True(block.Revision > revision);
        }
        /// <summary>Copy paths invalidate even when a later corrupt slot interrupts an already partially applied update.</summary>
        [Fact]
        public void CopyMatchingValues_InvalidatesTextureAndConstantChangesAndPartialFailure() {
            using var source = new MaterialPropertyBlock(CreateLayout());
            source.SetTexture(0, new TestRuntimeTexture());
            source.SetConstantBufferData(0, new byte[] { 5, 6, 7, 8 });
            var layout = CreateLayout();
            using var target = new MaterialPropertyBlock(layout);
            ulong revision = target.Revision;
            target.CopyMatchingValuesFrom(source);
            Assert.True(target.Revision > revision);
            Assert.Same(source.GetTexture(0), target.GetTexture(0));
            Assert.Equal(source.GetConstantBufferData(0), target.GetConstantBufferData(0));
            revision = target.Revision;
            layout.ConstantBufferBindings[0] = null;
            Assert.Throws<NullReferenceException>(() => target.CopyMatchingValuesFrom(source));
            Assert.True(target.Revision > revision);
        }
        /// <summary>Replacing the layout and disposing the material cannot reuse a prior property-block generation.</summary>
        [Fact]
        public void LayoutRevision_ChangesForReplacementAndDispose() {
            var material = new ShaderRuntimeMaterial();
            ulong revision = material.LayoutRevision;
            material.SetLayout(CreateLayout());
            Assert.True(material.LayoutRevision > revision); revision = material.LayoutRevision;
            MaterialPropertyBlock old = material.Properties;
            material.SetLayout(CreateLayout());
            Assert.True(material.LayoutRevision > revision);
            Assert.NotSame(old, material.Properties); revision = material.LayoutRevision;
            Assert.Throws<ArgumentNullException>(() => material.SetLayout(null));
            Assert.Equal(revision, material.LayoutRevision);
            material.Dispose();
            Assert.True(material.LayoutRevision > revision);
        }
        /// <summary>Refuses counter wrap before a write, preventing a stale cache hit after theoretical revision exhaustion.</summary>
        [Fact]
        public void RevisionOverflow_ThrowsBeforeMutation() {
            var block = new MaterialPropertyBlock(CreateLayout());
            typeof(MaterialPropertyBlock).GetField("RevisionValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(block, ulong.MaxValue);
            Assert.Throws<OverflowException>(() => block.SetTexture(0, new TestRuntimeTexture()));
            Assert.Null(block.GetTexture(0));
            var material = new ShaderRuntimeMaterial();
            typeof(ShaderRuntimeMaterial).GetField("LayoutRevisionValue", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(material, ulong.MaxValue);
            MaterialPropertyBlock old = material.Properties;
            Assert.Throws<OverflowException>(() => material.SetLayout(CreateLayout()));
            Assert.Same(old, material.Properties);
        }
        /// <summary>Builds one texture and one fixed-size constant binding so every mutation route is observable.</summary>
        static MaterialLayout CreateLayout() {
            return new MaterialLayout("shader", "VS", "PS", "default", new MaterialRenderState(),
                new[] { new MaterialLayoutBinding("Texture", ShaderResourceType.Texture2D, 0, 0, 0) },
                new[] { new MaterialLayoutBinding("Params", ShaderResourceType.ConstantBuffer, 0, 3, 4) },
                Array.Empty<MaterialLayoutBinding>());
        }
    }
}
