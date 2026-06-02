namespace helengine {
    /// <summary>
    /// Defines one authored vertically aligned capsule collider consumed by 3D physics runtimes.
    /// </summary>
    public sealed class CapsuleCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the authored capsule radius.
        /// </summary>
        float RadiusValue;

        /// <summary>
        /// Backing field for the authored full capsule height from tip to tip.
        /// </summary>
        float HeightValue;

        /// <summary>
        /// Initializes a new capsule collider with conservative unit dimensions.
        /// </summary>
        public CapsuleCollider3DComponent() {
            RadiusValue = 0.5f;
            HeightValue = 2f;
        }

        /// <summary>
        /// Gets or sets the spherical cap radius in local units.
        /// </summary>
        public float Radius {
            get { return RadiusValue; }
            set {
                if (value <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capsule collider radius must be greater than zero.");
                }
                if (HeightValue < value * 2f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capsule collider radius cannot exceed half of the full capsule height.");
                }

                RadiusValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the full capsule height from the bottom tip to the top tip in local units.
        /// </summary>
        public float Height {
            get { return HeightValue; }
            set {
                if (value <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capsule collider height must be greater than zero.");
                }
                if (value < RadiusValue * 2f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Capsule collider height must be at least twice the radius.");
                }

                HeightValue = value;
            }
        }
    }
}
