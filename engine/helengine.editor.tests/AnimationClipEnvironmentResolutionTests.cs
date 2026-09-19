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

        [Fact]
        public void ResolveForScope_WithGroupOnlyPath_ReturnsTheBaseClip() {
            AnimationClipAsset clip = CreateClipWithPlatformOverride("ds", "debug");
            AnimationClipPlatformResolutionService service = new AnimationClipPlatformResolutionService();

            AnimationClipAsset resolved = service.ResolveForScope(clip, EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld")));

            Assert.Same(clip, resolved);
        }

        /// <summary>
        /// Builds one clip with a single <see cref="AnimationClipPlatformOverrideAsset"/> replacing the whole
        /// clip for the supplied platform and environment.
        /// </summary>
        static AnimationClipAsset CreateClipWithPlatformOverride(string platformId, string environmentId) {
            return new AnimationClipAsset {
                Id = "Animations/GroupOnly.hanim",
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
                        PlatformId = platformId,
                        EnvironmentId = environmentId,
                        Mode = AnimationClipPlatformOverrideMode.ReplaceWholeClip,
                        PositionTracks = [
                            new PlatformPositionKeyframeTrackAsset {
                                Keyframes = [new PositionKeyframeAsset(0f, new float3(2f, 0f, 0f), AnimationInterpolationMode.Step)]
                            }
                        ]
                    }
                ]
            };
        }
    }
}
