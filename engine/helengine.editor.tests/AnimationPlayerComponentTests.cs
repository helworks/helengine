using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies clip playback behavior for the core animation player component.
    /// </summary>
    public class AnimationPlayerComponentTests {
        /// <summary>
        /// Ensures offset position tracks apply on top of the entity's captured local position.
        /// </summary>
        [Fact]
        public void Advance_WhenOffsetPositionTrackIsPlaying_AddsOffsetOnTopOfBaseLocalPosition() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            entity.LocalPosition = new float3(5f, 0f, 0f);
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Shake.hanim",
                Duration = 1f,
                PositionOffsetTracks = [
                    new PositionOffsetKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(2f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, false);
            component.Advance(0.5f);

            Assert.Equal(new float3(6f, 0f, 0f), entity.LocalPosition);
        }

        /// <summary>
        /// Ensures looping playback wraps time instead of clamping at the clip duration.
        /// </summary>
        [Fact]
        public void Advance_WhenLoopingPlaybackExceedsDuration_WrapsCurrentTime() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Loop.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(10f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, true);
            component.Advance(1.5f);

            Assert.True(component.IsPlaying);
            Assert.Equal(0.5f, component.CurrentTime, 3);
            Assert.Equal(new float3(5f, 0f, 0f), entity.LocalPosition);
        }

        /// <summary>
        /// Ensures stopping playback restores the captured local transform and clears active playback state.
        /// </summary>
        [Fact]
        public void Stop_WhenPlaybackIsActive_RestoresBaseTransformAndClearsPlaybackState() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            entity.LocalPosition = new float3(3f, 0f, 0f);
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Move.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(10f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, false);
            component.Advance(0.5f);
            component.Stop();

            Assert.False(component.IsPlaying);
            Assert.Equal(0f, component.CurrentTime);
            Assert.Null(component.CurrentClip);
            Assert.Equal(new float3(3f, 0f, 0f), entity.LocalPosition);
        }

        /// <summary>
        /// Ensures scale tracks write the resolved local scale onto the owning entity during playback.
        /// </summary>
        [Fact]
        public void Advance_WhenScaleTrackIsPlaying_UpdatesEntityLocalScale() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            entity.LocalScale = new float3(1f, 1f, 1f);
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Scale.hanim",
                Duration = 1f,
                ScaleTracks = [
                    new ScaleKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(1f, 1f, 1f), AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(2f, 3f, 1f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, false);
            component.Advance(0.5f);

            Assert.Equal(new float3(1.5f, 2f, 1f), entity.LocalScale);
        }

        /// <summary>
        /// Ensures rotation tracks write the resolved local orientation onto the owning entity during playback.
        /// </summary>
        [Fact]
        public void Advance_WhenRotationTrackIsPlaying_UpdatesEntityLocalOrientation() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            float4 endOrientation;
            float4.CreateFromYawPitchRoll(0f, 0f, (float)(Math.PI / 2d), out endOrientation);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Rotate.hanim",
                Duration = 1f,
                RotationTracks = [
                    new RotationKeyframeTrackAsset {
                        Keyframes = [
                            new RotationKeyframeAsset(0f, float4.Identity, AnimationInterpolationMode.Step),
                            new RotationKeyframeAsset(1f, endOrientation, AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, false);
            component.Advance(1f);

            Assert.Equal(endOrientation.X, entity.LocalOrientation.X, 3);
            Assert.Equal(endOrientation.Y, entity.LocalOrientation.Y, 3);
            Assert.Equal(endOrientation.Z, entity.LocalOrientation.Z, 3);
            Assert.Equal(endOrientation.W, entity.LocalOrientation.W, 3);
        }

        /// <summary>
        /// Ensures update-loop playback consumes the current core frame delta instead of a hardcoded fixed slice.
        /// </summary>
        [Fact]
        public void Update_WhenRunningInsideCoreUpdate_UsesCurrentCoreDeltaTime() {
            Core core = InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/RuntimeDelta.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(30f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, false);

            core.Update(1d / 30d);

            Assert.Equal(1f / 30f, component.CurrentTime, 3);
            Assert.Equal(new float3(1f, 0f, 0f), entity.LocalPosition);
        }

        /// <summary>
        /// Ensures one configured player can begin its authored clip automatically without a custom startup component.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenAutomaticPlaybackIsConfigured_StartsAssignedClip() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/AutoPlay.hanim",
                Duration = 1f,
                PositionOffsetTracks = [
                    new PositionOffsetKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(2f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };
            AnimationPlayerComponent component = new AnimationPlayerComponent {
                Clip = clip,
                PlayAutomatically = true,
                ShouldLoop = true
            };

            entity.AddComponent(component);

            Assert.True(component.IsPlaying);
            Assert.Same(clip, component.CurrentClip);
            Assert.Equal(0f, component.CurrentTime);
            Assert.Equal(float3.Zero, entity.LocalPosition);
        }

        /// <summary>
        /// Ensures playback can be rebased onto an externally rewritten entity position while preserving the currently sampled offset pose.
        /// </summary>
        [Fact]
        public void RebaseCurrentPoseToLocalTransform_WhenOffsetTrackIsPlaying_PreservesExternallyAssignedPosition() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Rebase.hanim",
                Duration = 1f,
                PositionOffsetTracks = [
                    new PositionOffsetKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(2f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };

            component.Play(clip, true);
            component.Advance(0.5f);
            entity.LocalPosition = new float3(20f, 0f, 0f);

            component.RebaseCurrentPoseToLocalTransform();
            component.Advance(0f);

            Assert.Equal(new float3(20f, 0f, 0f), entity.LocalPosition);
        }

        /// <summary>
        /// Ensures the first runtime slice rejects multiple transform tracks on the same channel because target bindings do not exist yet.
        /// </summary>
        [Fact]
        public void Play_WhenClipContainsMultiplePositionTracks_ThrowsInvalidOperationException() {
            InitializeCore();
            Entity entity = new Entity();
            entity.InitComponents();
            AnimationPlayerComponent component = new AnimationPlayerComponent();
            entity.AddComponent(component);
            AnimationClipAsset clip = new AnimationClipAsset {
                Id = "Animations/Unsupported.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, float3.Zero, AnimationInterpolationMode.Step)
                        ]
                    },
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(1f, 0f, 0f), AnimationInterpolationMode.Step)
                        ]
                    }
                ]
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(new Action(() => component.Play(clip, false)));

            Assert.Equal("Animation clips can currently bind only one track per transform channel.", exception.Message);
        }

        /// <summary>
        /// Initializes the minimal core services required by animation-player tests.
        /// </summary>
        Core InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
