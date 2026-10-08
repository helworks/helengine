using helengine;
using helengine.files;
using Xunit;

namespace helengine.files.tests.assets {
    /// <summary>
    /// Verifies that a multi-pass effect definition survives the HELE editor asset format intact, so built-in and
    /// project-authored <c>.heffect</c> files load with the same passes, targets and parameter slots they were saved with.
    /// </summary>
    public class EffectAssetPayloadSerializerTests {
        [Fact]
        public void Serialize_thenDeserialize_preservesEveryPassTargetAndParameter() {
            EffectAsset original = new EffectAsset {
                EffectId = "soft-shadow",
                DisplayName = "Soft Shadow",
                EffectVersion = 3,
                DowngradeMode = RendererFeatureDowngradeMode.Drop,
                Category = EffectCategory.Transition,
                Inputs = new[] { new EffectInputAsset("Source", true) },
                Targets = new[] { new EffectTargetAsset("BlurX", 0.5f, EffectTargetFormat.Rgba8) },
                Passes = new[] {
                    new EffectPassAsset { ShaderPath = "shaders/Blur.hlsl", PixelEntryPoint = "BlurPS", Reads = new[] { "Source" }, Writes = "BlurX", PassConstants = new float4(1, 0, 0, 0) },
                    new EffectPassAsset { ShaderPath = "shaders/Shadow.hlsl", PixelEntryPoint = "ShadowPS", Reads = new[] { "Source", "BlurX" }, Writes = EffectAsset.OutputTargetName, PassConstants = new float4(0, 1, 0, 0) }
                },
                Parameters = new[] {
                    new EffectParameterAsset { Name = "Color", Description = "Shadow tint.", Type = EffectParameterType.Color, DefaultValue = new float4(0, 0, 0, 0.6f), Minimum = 0, Maximum = 1, Slot = 4 },
                    new EffectParameterAsset { Name = "Mode", Type = EffectParameterType.Enum, DefaultValue = new float4(1, 0, 0, 0), Slot = 0, AllowedValues = new[] { "Hard", "Soft" } }
                }
            };

            using MemoryStream stream = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(stream, original);
            stream.Position = 0;
            EffectAsset restored = Assert.IsType<EffectAsset>(EditorAssetBinarySerializer.Deserialize(stream));

            Assert.Equal("soft-shadow", restored.EffectId);
            Assert.Equal("Soft Shadow", restored.DisplayName);
            Assert.Equal(3, restored.EffectVersion);
            Assert.Equal(RendererFeatureDowngradeMode.Drop, restored.DowngradeMode);
            Assert.Equal(EffectCategory.Transition, restored.Category);
            Assert.True(Assert.Single(restored.Inputs).RequiresAlpha);
            EffectTargetAsset target = Assert.Single(restored.Targets);
            Assert.Equal("BlurX", target.Name);
            Assert.Equal(0.5f, target.Scale);
            Assert.Equal(EffectTargetFormat.Rgba8, target.Format);
            Assert.Equal(2, restored.Passes.Length);
            Assert.Equal(new[] { "Source", "BlurX" }, restored.Passes[1].Reads);
            Assert.Equal(EffectAsset.OutputTargetName, restored.Passes[1].Writes);
            Assert.Equal(1f, restored.Passes[0].PassConstants.X);
            Assert.Equal(0.6f, restored.Parameters[0].DefaultValue.W);
            Assert.Equal(4, restored.Parameters[0].Slot);
            Assert.Equal(new[] { "Hard", "Soft" }, restored.Parameters[1].AllowedValues);
        }

        [Fact]
        public void Serialize_whenEffectIdIsMissing_throwsBeforeWriting() {
            using MemoryStream stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Serialize(stream, new EffectAsset()));
            Assert.Equal(0, stream.Length);
        }
    }
}
