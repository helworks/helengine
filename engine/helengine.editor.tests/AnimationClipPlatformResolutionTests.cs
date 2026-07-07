using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies cook-time resolution of animation clip platform override modes.
    /// </summary>
    public sealed class AnimationClipPlatformResolutionTests {
        readonly AnimationClipPlatformResolutionService Service = new AnimationClipPlatformResolutionService();

        /// <summary>
        /// Ensures inherit-base mode keeps the authored base timeline and strips platform override payloads from the resolved clip.
        /// </summary>
        [Fact]
        public void ResolveForPlatform_WhenModeIsInheritBase_ReturnsBaseTimeline() {
            AnimationClipAsset clip = CreateBaseClipWithDsInherit();

            AnimationClipAsset resolved = Service.ResolveForPlatform(clip, "ds");

            Assert.Empty(resolved.PlatformOverrides);
            Assert.Equal("base-pos-001", resolved.PositionTracks[0].Keyframes[1].FrameId);
            Assert.Equal(2, resolved.PositionTracks[0].Keyframes.Length);
        }

        /// <summary>
        /// Ensures override-frames mode merges base, overridden, and inserted frames into one timestamp-sorted resolved timeline.
        /// </summary>
        [Fact]
        public void ResolveForPlatform_WhenModeIsOverrideFrames_MergesAndSortsByTimestamp() {
            AnimationClipAsset clip = CreateClipWithDsOverrides();

            AnimationClipAsset resolved = Service.ResolveForPlatform(clip, "ds");

            PositionKeyframeAsset[] keyframes = resolved.PositionTracks[0].Keyframes;
            Assert.Collection(
                keyframes,
                keyframe => Assert.Equal(0f, keyframe.Time),
                keyframe => Assert.Equal(0.5f, keyframe.Time),
                keyframe => Assert.Equal(1f, keyframe.Time));
            Assert.Equal(new float3(8f, -4f, 0f), keyframes[2].Value);
            Assert.All(keyframes, keyframe => Assert.True(string.IsNullOrEmpty(keyframe.FrameId)));
            Assert.Empty(resolved.PlatformOverrides);
        }

        /// <summary>
        /// Creates one clip whose DS platform stays on the base timeline.
        /// </summary>
        /// <returns>Clip using inherit-base resolution for Nintendo DS.</returns>
        AnimationClipAsset CreateBaseClipWithDsInherit() {
            return new AnimationClipAsset {
                Id = "Animations/TestPlatformResolveInherit.hanim",
                Duration = 1f,
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
                        Mode = AnimationClipPlatformOverrideMode.InheritBase
                    }
                ]
            };
        }

        /// <summary>
        /// Creates one clip whose DS platform overrides one base frame and inserts one additional frame.
        /// </summary>
        /// <returns>Clip using override-frame resolution for Nintendo DS.</returns>
        AnimationClipAsset CreateClipWithDsOverrides() {
            return new AnimationClipAsset {
                Id = "Animations/TestPlatformResolveOverride.hanim",
                Duration = 1f,
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
        }
    }
}
