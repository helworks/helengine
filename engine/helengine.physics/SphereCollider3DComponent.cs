namespace helengine {
    /// <summary>
    /// Defines one authored sphere collider consumed by 3D physics runtimes.
    /// </summary>
    public sealed class SphereCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the authored sphere radius.
        /// </summary>
        float RadiusValue;

        /// <summary>
        /// Initializes a new sphere collider with a unit radius.
        /// </summary>
        public SphereCollider3DComponent() {
            RadiusValue = 0.5f;
        }

        /// <summary>
        /// Gets or sets the sphere radius in local units.
        /// </summary>
        public float Radius {
            get { return RadiusValue; }
            set {
                if (value <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Sphere collider radius must be greater than zero.");
                }

                RadiusValue = value;
            }
        }
    }
}
