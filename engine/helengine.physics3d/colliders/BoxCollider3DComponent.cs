namespace helengine {
    /// <summary>
    /// Defines one authored axis-aligned box collider consumed by the 3D physics runtime.
    /// </summary>
    public sealed class BoxCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the authored box size.
        /// </summary>
        float3 SizeValue;

        /// <summary>
        /// Initializes a new box collider with unit dimensions.
        /// </summary>
        public BoxCollider3DComponent() {
            SizeValue = float3.One;
        }

        /// <summary>
        /// Gets or sets the full box size in local units.
        /// </summary>
        public float3 Size {
            get { return SizeValue; }
            set {
                if (value.X <= 0f || value.Y <= 0f || value.Z <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Box collider sizes must be greater than zero on every axis.");
                }

                SizeValue = value;
            }
        }
    }
}
