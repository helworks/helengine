namespace helengine {
    /// <summary>Immutable system typography request; font files remain owned by platform APIs.</summary>
    public sealed class SystemTextStyle {
        /// <summary>Creates a semantic style. A zero point size selects the role's preferred base size; positive values override the base while retaining system text-size scaling.</summary>
        public SystemTextStyle(SystemTextRole role, SystemTextWeight weight = SystemTextWeight.Regular, bool rounded = false, float pointSize = 0f) {
            if (role < SystemTextRole.LargeTitle || role > SystemTextRole.Caption) {
                throw new ArgumentOutOfRangeException(nameof(role));
            }
            if (weight < SystemTextWeight.Regular || weight > SystemTextWeight.Bold) {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }
            if (float.IsNaN(pointSize) || float.IsInfinity(pointSize) || pointSize < 0f || pointSize > 256f) {
                throw new ArgumentOutOfRangeException(nameof(pointSize));
            }
            Role = role;
            Weight = weight;
            Rounded = rounded;
            PointSize = pointSize;
        }
        /// <summary>Gets the semantic preferred-size role.</summary>
        public SystemTextRole Role { get; }
        /// <summary>Gets the requested system weight.</summary>
        public SystemTextWeight Weight { get; }
        /// <summary>Gets whether the platform's rounded system design is requested.</summary>
        public bool Rounded { get; }
        /// <summary>Gets the base size in reference-canvas units, or zero for the role default; viewport scaling is applied separately.</summary>
        public float PointSize { get; }
    }
}
