namespace helengine.animation.runtime {
    /// <summary>
    /// Samples a <see cref="HierarchyAnimationClipAsset"/> at one time and writes the local transforms of the entities its
    /// tracks drive. Sampling depends only on the time, never on earlier samples, so seeking back and forth is stable.
    /// Before the first key a channel holds the first value and after the last key it holds the last value.
    /// </summary>
    public static class HierarchyAnimationEvaluator {
        /// <summary>
        /// Applies every track of a clip at one time.
        /// </summary>
        /// <param name="clip">Clip to sample.</param>
        /// <param name="targets">Entity driven by each track, aligned index for index with <see cref="HierarchyAnimationClipAsset.Tracks"/>.</param>
        /// <param name="time">Time in seconds.</param>
        public static void Apply([NativeNoEscape] HierarchyAnimationClipAsset clip, [NativeNoEscape] Entity[] targets, double time) {
            if (clip == null) {
                throw new ArgumentNullException(nameof(clip));
            } else if (targets == null) {
                throw new ArgumentNullException(nameof(targets));
            } else if (targets.Length != clip.Tracks.Length) {
                throw new InvalidOperationException($"Hierarchy animation clip has {clip.Tracks.Length} tracks, but {targets.Length} targets were supplied.");
            }

            for (int index = 0; index < clip.Tracks.Length; index++) {
                HierarchyAnimationTrack track = clip.Tracks[index];
                Entity target = targets[index];
                if (target == null) {
                    throw new InvalidOperationException($"Hierarchy animation track '{track.TargetPath}' has no target entity.");
                }

                if (track.PositionTimes.Length > 0) {
                    target.LocalPosition = SampleFloat3(track.PositionTimes, track.PositionValues, track.Interpolation, time);
                }
                if (track.RotationTimes.Length > 0) {
                    target.LocalOrientation = SampleRotation(track.RotationTimes, track.RotationValues, track.Interpolation, time);
                }
                if (track.ScaleTimes.Length > 0) {
                    target.LocalScale = SampleFloat3(track.ScaleTimes, track.ScaleValues, track.Interpolation, time);
                }
            }
        }

        /// <summary>
        /// Samples a position or scale channel, interpolating linearly unless the mode is step.
        /// </summary>
        /// <param name="times">Strictly increasing key times.</param>
        /// <param name="values">Value at each key.</param>
        /// <param name="mode">Interpolation mode.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The sampled value.</returns>
        static float3 SampleFloat3([NativeNoEscape] double[] times, [NativeNoEscape] float3[] values, AnimationInterpolationMode mode, double time) {
            int key = FindKey(times, time);
            if (key < 0) {
                return values[0];
            } else if (key == times.Length - 1 || mode == AnimationInterpolationMode.Step) {
                return values[key];
            }

            double amount = (time - times[key]) / (times[key + 1] - times[key]);
            return float3.Lerp(values[key], values[key + 1], amount);
        }

        /// <summary>
        /// Samples a rotation channel, interpolating spherically unless the mode is step.
        /// </summary>
        /// <param name="times">Strictly increasing key times.</param>
        /// <param name="values">Quaternion at each key.</param>
        /// <param name="mode">Interpolation mode.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The sampled orientation.</returns>
        static float4 SampleRotation([NativeNoEscape] double[] times, [NativeNoEscape] float4[] values, AnimationInterpolationMode mode, double time) {
            int key = FindKey(times, time);
            if (key < 0) {
                return values[0];
            } else if (key == times.Length - 1 || mode == AnimationInterpolationMode.Step) {
                return values[key];
            }

            double amount = (time - times[key]) / (times[key + 1] - times[key]);
            return float4.Slerp(values[key], values[key + 1], amount);
        }

        /// <summary>
        /// Binary-searches the last key whose time is at or before a time.
        /// </summary>
        /// <param name="times">Strictly increasing key times.</param>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The key index, or -1 when the time is before the first key.</returns>
        static int FindKey([NativeNoEscape] double[] times, double time) {
            int low = 0;
            int high = times.Length - 1;
            int found = -1;
            while (low <= high) {
                int middle = low + ((high - low) / 2);
                if (times[middle] <= time) {
                    found = middle;
                    low = middle + 1;
                } else {
                    high = middle - 1;
                }
            }

            return found;
        }
    }
}
