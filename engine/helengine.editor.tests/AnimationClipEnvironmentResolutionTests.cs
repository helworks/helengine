namespace helengine.editor.tests {
    public sealed class AnimationClipEnvironmentResolutionTests {
        [Fact]
        public void ResolveForScope_AppliesEnvironmentOverrideAfterPlatformResolution() {
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Environment.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(1f, 0f, 0f), AnimationInterpolationMode.Step) {
                                FrameId = "base"
                            }
                        ]
                    }
                ],
                PlatformOverrides = [
                    new AnimationClipPlatformOverrideAsset {
                        PlatformId = "windows",
                        Mode = AnimationClipPlatformOverrideMode.ReplaceWholeClip,
                        PositionTracks = [
                            new PlatformPositionKeyframeTrackAsset {
                                Keyframes = [new PositionKeyframeAsset(0f, new float3(2f, 0f, 0f), AnimationInterpolationMode.Step)]
                            }
                        ]
                    },
                    new AnimationClipPlatformOverrideAsset {
                        PlatformId = "windows",
                        EnvironmentId = "debug",
                        Mode = AnimationClipPlatformOverrideMode.ReplaceWholeClip,
                        PositionTracks = [
                            new PlatformPositionKeyframeTrackAsset {
                                Keyframes = [new PositionKeyframeAsset(0f, new float3(3f, 0f, 0f), AnimationInterpolationMode.Step)]
                            }
                        ]
                    }
                ]
            };

            AnimationClipAsset resolved = new AnimationClipPlatformResolutionService().ResolveForScope(
                clip,
                new EditorOverrideScope("windows", "debug"));

            Assert.Equal(new float3(3f, 0f, 0f), resolved.PositionTracks[0].Keyframes[0].Value);
            Assert.Empty(resolved.PlatformOverrides);
        }
    }
}
