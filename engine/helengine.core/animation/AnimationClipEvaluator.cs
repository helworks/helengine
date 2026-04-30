namespace helengine {
    /// <summary>
    /// Evaluates typed animation keyframe tracks at a specific playback time without mutating scene state.
    /// </summary>
    public static class AnimationClipEvaluator {
        /// <summary>
        /// Evaluates an absolute or additive position-style track at the supplied playback time.
        /// </summary>
        /// <param name="track">Track to evaluate.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated vector value for the requested time.</returns>
        public static float3 EvaluatePositionTrack(PositionKeyframeTrackAsset track, float time) {
            if (track == null) {
                throw new ArgumentNullException(nameof(track));
            } else if (track.Keyframes == null || track.Keyframes.Length == 0) {
                throw new InvalidOperationException("Animation position tracks must contain at least one keyframe.");
            }

            PositionKeyframeAsset firstKeyframe = track.Keyframes[0];
            if (time <= firstKeyframe.Time) {
                return firstKeyframe.Value;
            }

            PositionKeyframeAsset lastKeyframe = track.Keyframes[track.Keyframes.Length - 1];
            if (time >= lastKeyframe.Time) {
                return lastKeyframe.Value;
            }

            for (int i = 0; i < track.Keyframes.Length - 1; i++) {
                PositionKeyframeAsset startKeyframe = track.Keyframes[i];
                PositionKeyframeAsset endKeyframe = track.Keyframes[i + 1];
                if (time <= endKeyframe.Time) {
                    return InterpolatePositionKeyframes(startKeyframe, endKeyframe, time);
                }
            }

            return lastKeyframe.Value;
        }

        /// <summary>
        /// Evaluates an additive-position track at the supplied playback time.
        /// </summary>
        /// <param name="track">Track to evaluate.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated vector value for the requested time.</returns>
        public static float3 EvaluatePositionTrack(PositionOffsetKeyframeTrackAsset track, float time) {
            if (track == null) {
                throw new ArgumentNullException(nameof(track));
            }

            return EvaluatePositionKeyframes(track.Keyframes, time, "Animation additive-position tracks must contain at least one keyframe.");
        }

        /// <summary>
        /// Evaluates a scale track at the supplied playback time.
        /// </summary>
        /// <param name="track">Track to evaluate.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated vector value for the requested time.</returns>
        public static float3 EvaluatePositionTrack(ScaleKeyframeTrackAsset track, float time) {
            if (track == null) {
                throw new ArgumentNullException(nameof(track));
            }

            return EvaluatePositionKeyframes(track.Keyframes, time, "Animation scale tracks must contain at least one keyframe.");
        }

        /// <summary>
        /// Evaluates a rotation track at the supplied playback time.
        /// </summary>
        /// <param name="track">Track to evaluate.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated quaternion value for the requested time.</returns>
        public static float4 EvaluateRotationTrack(RotationKeyframeTrackAsset track, float time) {
            if (track == null) {
                throw new ArgumentNullException(nameof(track));
            } else if (track.Keyframes == null || track.Keyframes.Length == 0) {
                throw new InvalidOperationException("Animation rotation tracks must contain at least one keyframe.");
            }

            RotationKeyframeAsset firstKeyframe = track.Keyframes[0];
            if (time <= firstKeyframe.Time) {
                return firstKeyframe.Value;
            }

            RotationKeyframeAsset lastKeyframe = track.Keyframes[track.Keyframes.Length - 1];
            if (time >= lastKeyframe.Time) {
                return lastKeyframe.Value;
            }

            for (int i = 0; i < track.Keyframes.Length - 1; i++) {
                RotationKeyframeAsset startKeyframe = track.Keyframes[i];
                RotationKeyframeAsset endKeyframe = track.Keyframes[i + 1];
                if (time <= endKeyframe.Time) {
                    return InterpolateRotationKeyframes(startKeyframe, endKeyframe, time);
                }
            }

            return lastKeyframe.Value;
        }

        /// <summary>
        /// Evaluates a position-style keyframe collection at the supplied playback time.
        /// </summary>
        /// <param name="keyframes">Ordered keyframes to evaluate.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <param name="emptyTrackMessage">Message used when the collection is empty.</param>
        /// <returns>Interpolated vector value.</returns>
        static float3 EvaluatePositionKeyframes(PositionKeyframeAsset[] keyframes, float time, string emptyTrackMessage) {
            if (keyframes == null || keyframes.Length == 0) {
                throw new InvalidOperationException(emptyTrackMessage);
            }

            PositionKeyframeAsset firstKeyframe = keyframes[0];
            if (time <= firstKeyframe.Time) {
                return firstKeyframe.Value;
            }

            PositionKeyframeAsset lastKeyframe = keyframes[keyframes.Length - 1];
            if (time >= lastKeyframe.Time) {
                return lastKeyframe.Value;
            }

            for (int i = 0; i < keyframes.Length - 1; i++) {
                PositionKeyframeAsset startKeyframe = keyframes[i];
                PositionKeyframeAsset endKeyframe = keyframes[i + 1];
                if (time <= endKeyframe.Time) {
                    return InterpolatePositionKeyframes(startKeyframe, endKeyframe, time);
                }
            }

            return lastKeyframe.Value;
        }

        /// <summary>
        /// Evaluates one position-style keyframe segment for the supplied time.
        /// </summary>
        /// <param name="startKeyframe">Segment start keyframe.</param>
        /// <param name="endKeyframe">Segment end keyframe.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated vector value.</returns>
        static float3 InterpolatePositionKeyframes(PositionKeyframeAsset startKeyframe, PositionKeyframeAsset endKeyframe, float time) {
            if (endKeyframe.Time <= startKeyframe.Time) {
                return endKeyframe.Value;
            } else if (endKeyframe.InterpolationMode == AnimationInterpolationMode.Step) {
                return startKeyframe.Value;
            }

            float amount = (time - startKeyframe.Time) / (endKeyframe.Time - startKeyframe.Time);
            return float3.Lerp(startKeyframe.Value, endKeyframe.Value, amount);
        }

        /// <summary>
        /// Evaluates one rotation keyframe segment for the supplied time.
        /// </summary>
        /// <param name="startKeyframe">Segment start keyframe.</param>
        /// <param name="endKeyframe">Segment end keyframe.</param>
        /// <param name="time">Playback time in seconds.</param>
        /// <returns>Interpolated quaternion value.</returns>
        static float4 InterpolateRotationKeyframes(RotationKeyframeAsset startKeyframe, RotationKeyframeAsset endKeyframe, float time) {
            if (endKeyframe.Time <= startKeyframe.Time) {
                return endKeyframe.Value;
            } else if (endKeyframe.InterpolationMode == AnimationInterpolationMode.Step) {
                return startKeyframe.Value;
            }

            float amount = (time - startKeyframe.Time) / (endKeyframe.Time - startKeyframe.Time);
            return float4.Lerp(startKeyframe.Value, endKeyframe.Value, amount);
        }
    }
}
