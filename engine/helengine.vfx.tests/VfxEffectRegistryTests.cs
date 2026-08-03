using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    class FakeEffect : IVfxEffect {
        public string Id => "fake-effect";
        public string DisplayName => "Fake Effect";
        public IReadOnlyList<VfxEffectParameterDescriptor> Parameters { get; } = new List<VfxEffectParameterDescriptor>();
        public string ShaderResourcePath => "shaders/fake.hlsl";
        public string VertexEntryPoint => "FullscreenVS";
        public string PixelEntryPoint => "FakePS";
        public float[] ResolveParameterSlots(IReadOnlyDictionary<string, string> parameterValues) => new float[VfxFrameConstants.ParamSlotCount];
    }

    public class VfxEffectRegistryTests {
        [Fact]
        public void Resolve_RegisteredId_ReturnsEffect() {
            VfxEffectRegistry.Register(new FakeEffect());

            IVfxEffect resolved = VfxEffectRegistry.Resolve("fake-effect");

            Assert.Equal("fake-effect", resolved.Id);
        }

        [Fact]
        public void Resolve_UnknownId_ThrowsWithMessage() {
            var exception = Assert.Throws<InvalidOperationException>(() => VfxEffectRegistry.Resolve("does-not-exist"));

            Assert.Contains("does-not-exist", exception.Message);
        }
    }
}
