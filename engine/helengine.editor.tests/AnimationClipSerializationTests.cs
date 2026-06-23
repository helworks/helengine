using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies HELE serialization behavior for animation clip assets and their typed track payloads.
    /// </summary>
    public class AnimationClipSerializationTests {
        /// <summary>
        /// Ensures animation clip assets round-trip through the HELE serializer with all typed transform tracks intact.
        /// </summary>
        [Fact]
        public void AssetSerializer_AnimationClipAsset_RoundTripsTypedTracks() {
            AnimationClipAsset asset = CreateAnimationClipAsset();

            byte[] data = AssetSerializer.SerializeToBytes(asset);
            EngineBinaryHeader header = ReadHeader(data);
            AnimationClipAsset deserialized = (AnimationClipAsset)AssetSerializer.DeserializeFromBytes(data);

            Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
            Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
            Assert.Equal((ushort)EditorAssetBinaryValueKind.AnimationClipAsset, header.ValueKind);
            Assert.Equal(asset.Id, deserialized.Id);
            Assert.Equal(asset.Duration, deserialized.Duration);

            PositionKeyframeTrackAsset positionTrack = Assert.Single(deserialized.PositionTracks);
            Assert.Collection(
                positionTrack.Keyframes,
                keyframe => {
                    Assert.Equal(0f, keyframe.Time);
                    Assert.Equal(new float3(0f, 0f, 0f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Step, keyframe.InterpolationMode);
                },
                keyframe => {
                    Assert.Equal(1f, keyframe.Time);
                    Assert.Equal(new float3(10f, 4f, -2f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Linear, keyframe.InterpolationMode);
                });

            PositionOffsetKeyframeTrackAsset offsetTrack = Assert.Single(deserialized.PositionOffsetTracks);
            Assert.Collection(
                offsetTrack.Keyframes,
                keyframe => {
                    Assert.Equal(0f, keyframe.Time);
                    Assert.Equal(new float3(0f, 0f, 0f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Linear, keyframe.InterpolationMode);
                },
                keyframe => {
                    Assert.Equal(1f, keyframe.Time);
                    Assert.Equal(new float3(1f, 0f, 0f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Linear, keyframe.InterpolationMode);
                });

            ScaleKeyframeTrackAsset scaleTrack = Assert.Single(deserialized.ScaleTracks);
            Assert.Collection(
                scaleTrack.Keyframes,
                keyframe => {
                    Assert.Equal(0f, keyframe.Time);
                    Assert.Equal(new float3(1f, 1f, 1f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Step, keyframe.InterpolationMode);
                },
                keyframe => {
                    Assert.Equal(1f, keyframe.Time);
                    Assert.Equal(new float3(2f, 2f, 2f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Linear, keyframe.InterpolationMode);
                });

            RotationKeyframeTrackAsset rotationTrack = Assert.Single(deserialized.RotationTracks);
            Assert.Collection(
                rotationTrack.Keyframes,
                keyframe => {
                    Assert.Equal(0f, keyframe.Time);
                    Assert.Equal(float4.Identity, keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Step, keyframe.InterpolationMode);
                },
                keyframe => {
                    Assert.Equal(1f, keyframe.Time);
                    Assert.Equal(new float4(0f, 0.70710677f, 0f, 0.70710677f), keyframe.Value);
                    Assert.Equal(AnimationInterpolationMode.Linear, keyframe.InterpolationMode);
                });
        }

        /// <summary>
        /// Creates one deterministic animation clip asset containing all first-slice transform track types.
        /// </summary>
        /// <returns>Animation clip asset with stable typed track payloads.</returns>
        AnimationClipAsset CreateAnimationClipAsset() {
            return new AnimationClipAsset {
                Id = "Animations/Test.hanim",
                Duration = 1f,
                PositionTracks = [
                    new PositionKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(0f, 0f, 0f), AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(10f, 4f, -2f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ],
                PositionOffsetTracks = [
                    new PositionOffsetKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(0f, 0f, 0f), AnimationInterpolationMode.Linear),
                            new PositionKeyframeAsset(1f, new float3(1f, 0f, 0f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ],
                ScaleTracks = [
                    new ScaleKeyframeTrackAsset {
                        Keyframes = [
                            new PositionKeyframeAsset(0f, new float3(1f, 1f, 1f), AnimationInterpolationMode.Step),
                            new PositionKeyframeAsset(1f, new float3(2f, 2f, 2f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ],
                RotationTracks = [
                    new RotationKeyframeTrackAsset {
                        Keyframes = [
                            new RotationKeyframeAsset(0f, float4.Identity, AnimationInterpolationMode.Step),
                            new RotationKeyframeAsset(1f, new float4(0f, 0.70710677f, 0f, 0.70710677f), AnimationInterpolationMode.Linear)
                        ]
                    }
                ]
            };
        }

        /// <summary>
        /// Reads the shared HELE header from a serialized asset byte array.
        /// </summary>
        /// <param name="data">Serialized asset bytes.</param>
        /// <returns>Decoded engine binary header.</returns>
        EngineBinaryHeader ReadHeader(byte[] data) {
            using MemoryStream stream = new MemoryStream(data, false);
            return EngineBinaryHeaderSerializer.Read(stream);
        }
    }
}
