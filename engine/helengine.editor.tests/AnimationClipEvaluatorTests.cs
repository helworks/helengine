using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies deterministic keyframe evaluation for the first animation clip track types.
    /// </summary>
    public class AnimationClipEvaluatorTests {
        /// <summary>
        /// Ensures linear interpolation blends between position keyframes by normalized segment progress.
        /// </summary>
        [Fact]
        public void EvaluatePositionTrack_WhenLinearKeyframesInterpolatesBetweenValues() {
            PositionKeyframeTrackAsset track = new PositionKeyframeTrackAsset {
                Keyframes = [
                    new PositionKeyframeAsset(0f, new float3(0f, 0f, 0f), AnimationInterpolationMode.Step),
                    new PositionKeyframeAsset(2f, new float3(10f, 4f, -2f), AnimationInterpolationMode.Linear)
                ]
            };

            float3 value = AnimationClipEvaluator.EvaluatePositionTrack(track, 1f);

            Assert.Equal(new float3(5f, 2f, -1f), value);
        }

        /// <summary>
        /// Ensures step interpolation holds the previous keyframe value until the next keyframe time is reached.
        /// </summary>
        [Fact]
        public void EvaluatePositionTrack_WhenBetweenStepKeyframesHoldsPreviousValue() {
            PositionKeyframeTrackAsset track = new PositionKeyframeTrackAsset {
                Keyframes = [
                    new PositionKeyframeAsset(0f, new float3(1f, 2f, 3f), AnimationInterpolationMode.Step),
                    new PositionKeyframeAsset(1f, new float3(9f, 8f, 7f), AnimationInterpolationMode.Step)
                ]
            };

            float3 value = AnimationClipEvaluator.EvaluatePositionTrack(track, 0.75f);

            Assert.Equal(new float3(1f, 2f, 3f), value);
        }

        /// <summary>
        /// Ensures quaternion interpolation returns a normalized result instead of a drifted linear average.
        /// </summary>
        [Fact]
        public void EvaluateRotationTrack_WhenLinearKeyframesInterpolatesAndNormalizesQuaternion() {
            RotationKeyframeTrackAsset track = new RotationKeyframeTrackAsset {
                Keyframes = [
                    new RotationKeyframeAsset(0f, float4.Identity, AnimationInterpolationMode.Step),
                    new RotationKeyframeAsset(1f, new float4(0f, 1f, 0f, 0f), AnimationInterpolationMode.Linear)
                ]
            };

            float4 value = AnimationClipEvaluator.EvaluateRotationTrack(track, 0.5f);
            double length = Math.Sqrt(
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z) +
                (value.W * value.W));

            Assert.Equal(0d, value.X, 3);
            Assert.Equal(0.707d, value.Y, 3);
            Assert.Equal(0d, value.Z, 3);
            Assert.Equal(0.707d, value.W, 3);
            Assert.Equal(1d, length, 3);
        }
    }
}
