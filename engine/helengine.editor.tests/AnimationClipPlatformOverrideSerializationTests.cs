using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies animation clips round-trip editor-authored platform override data.
    /// </summary>
    public sealed class AnimationClipPlatformOverrideSerializationTests {
        /// <summary>
        /// Ensures base frames, editor-only frame ids, and per-platform override payloads survive serialization.
        /// </summary>
        [Fact]
        public void AssetSerializer_AnimationClipAsset_RoundTripsPlatformOverrides() {
            AnimationClipAsset asset = new AnimationClipAsset {
                Id = "Animations/TestPlatformOverrides.hanim",
                Duration = 1.25f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(0f, 0f, 0f), AnimationInterpolationMode.Step) {
                                FrameId = "base-pos-000"
                            },
                            new PositionKeyframeAsset(1f, new float3(16f, 0f, 0f), AnimationInterpolationMode.Linear) {
                                FrameId = "base-pos-001"
                            }
                        ]
                    }
                ],
                PlatformOverrides = [
                    new AnimationClipPlatformOverrideAsset {
                        PlatformId = "ds",
                        Mode = AnimationClipPlatformOverrideMode.OverrideFrames,
                        PositionTracks = [
                            new PlatformPositionKeyframeTrackAsset {
                                Keyframes = [
                                    new PositionKeyframeAsset(1f, new float3(8f, -4f, 0f), AnimationInterpolationMode.Linear) {
                                        FrameId = "base-pos-001"
                                    },
                                    new PositionKeyframeAsset(0.5f, new float3(3f, 2f, 0f), AnimationInterpolationMode.Step) {
                                        FrameId = "ds-insert-000"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            AnimationClipAsset deserialized = Assert.IsType<AnimationClipAsset>(AssetSerializer.DeserializeFromBytes(data));

            Assert.Equal("base-pos-000", deserialized.PositionTracks[0].Keyframes[0].FrameId);
            AnimationClipPlatformOverrideAsset dsOverride = Assert.Single(deserialized.PlatformOverrides);
            Assert.Equal("ds", dsOverride.PlatformId);
            Assert.Equal(AnimationClipPlatformOverrideMode.OverrideFrames, dsOverride.Mode);
            Assert.Equal("base-pos-001", dsOverride.PositionTracks[0].Keyframes[0].FrameId);
            Assert.Equal("ds-insert-000", dsOverride.PositionTracks[0].Keyframes[1].FrameId);
        }
    }
}
