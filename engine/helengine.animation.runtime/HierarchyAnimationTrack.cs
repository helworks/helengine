namespace helengine.animation.runtime {
    /// <summary>
    /// Keyframes that animate one entity of a hierarchy: independent position, rotation and scale key lists, each with
    /// strictly increasing times in seconds and one value per time. An empty list leaves that part of the transform alone.
    /// </summary>
    public class HierarchyAnimationTrack : IDisposable {
        /// <summary>
        /// Gets or sets the slash-separated node path of the animated entity below the player's entity, as authored in the
        /// source scene. The runtime binds tracks through <see cref="HierarchyAnimationPlayerComponent.TrackTargets"/>; the
        /// path names the track in diagnostics and lets importers find the entity to bind.
        /// </summary>
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the position key times in seconds.
        /// </summary>
        public double[] PositionTimes { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the local position at each position key.
        /// </summary>
        public float3[] PositionValues { get; set; } = Array.Empty<float3>();

        /// <summary>
        /// Gets or sets the rotation key times in seconds.
        /// </summary>
        public double[] RotationTimes { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the local orientation quaternion at each rotation key.
        /// </summary>
        public float4[] RotationValues { get; set; } = Array.Empty<float4>();

        /// <summary>
        /// Gets or sets the scale key times in seconds.
        /// </summary>
        public double[] ScaleTimes { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Gets or sets the local scale at each scale key.
        /// </summary>
        public float3[] ScaleValues { get; set; } = Array.Empty<float3>();

        /// <summary>
        /// Gets or sets how keys blend: <see cref="AnimationInterpolationMode.Linear"/> interpolates position and scale
        /// linearly and rotation spherically; <see cref="AnimationInterpolationMode.Step"/> holds the earlier key.
        /// </summary>
        public AnimationInterpolationMode Interpolation { get; set; } = AnimationInterpolationMode.Linear;

        /// <summary>
        /// Releases the key arrays once no player uses the clip.
        /// </summary>
        public void Dispose() {
            double[] positionTimes = PositionTimes;
            float3[] positionValues = PositionValues;
            double[] rotationTimes = RotationTimes;
            float4[] rotationValues = RotationValues;
            double[] scaleTimes = ScaleTimes;
            float3[] scaleValues = ScaleValues;
            PositionTimes = null;
            PositionValues = null;
            RotationTimes = null;
            RotationValues = null;
            ScaleTimes = null;
            ScaleValues = null;
            DeleteOwnedArray(positionTimes);
            DeleteOwnedArray(positionValues);
            DeleteOwnedArray(rotationTimes);
            DeleteOwnedArray(rotationValues);
            DeleteOwnedArray(scaleTimes);
            DeleteOwnedArray(scaleValues);
        }

        /// <summary>
        /// Deletes one track-owned key array while preserving the shared empty-array singleton.
        /// </summary>
        /// <typeparam name="T">Value type stored in the array.</typeparam>
        /// <param name="values">Key array owned by this track.</param>
        static void DeleteOwnedArray<T>(T[] values) {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            NativeOwnership.Delete(values);
        }
    }
}
