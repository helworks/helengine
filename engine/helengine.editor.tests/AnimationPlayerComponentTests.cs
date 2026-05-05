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
                Id = "Animations/Shake.animation",
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
                Id = "Animations/Loop.animation",
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
                Id = "Animations/Move.animation",
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
                Id = "Animations/Unsupported.animation",
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
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
        }
    }
}
