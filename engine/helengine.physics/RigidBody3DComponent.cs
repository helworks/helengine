namespace helengine {
    /// <summary>
    /// Stores the authored rigid-body state consumed by 3D physics runtimes.
    /// </summary>
    public sealed class RigidBody3DComponent : Component {
        /// <summary>
        /// Backing field for the configured body kind.
        /// </summary>
        BodyKind3D BodyKindValue;

        /// <summary>
        /// Backing field for the authored linear velocity.
        /// </summary>
        float3 LinearVelocityValue;

        /// <summary>
        /// Backing field for the authored angular velocity.
        /// </summary>
        float3 AngularVelocityValue;

        /// <summary>
        /// Backing field for the authored mass value.
        /// </summary>
        double MassValue;

        /// <summary>
        /// Backing field for the authored gravity scale.
        /// </summary>
        double GravityScaleValue;

        /// <summary>
        /// Initializes a new rigid body with dynamic defaults suitable for general gameplay bodies.
        /// </summary>
        public RigidBody3DComponent() {
            BodyKindValue = BodyKind3D.Dynamic;
            LinearVelocityValue = float3.Zero;
            AngularVelocityValue = float3.Zero;
            UseGravity = true;
            MassValue = 1d;
            GravityScaleValue = 1d;
        }

        /// <summary>
        /// Gets or sets the simulation participation mode for the body.
        /// </summary>
        public BodyKind3D BodyKind {
            get { return BodyKindValue; }
            set { BodyKindValue = value; }
        }

        /// <summary>
        /// Gets or sets the authored linear velocity in world units per second.
        /// </summary>
        public float3 LinearVelocity {
            get { return LinearVelocityValue; }
            set { LinearVelocityValue = value; }
        }

        /// <summary>
        /// Replaces the authored linear velocity with the supplied world-space value.
        /// </summary>
        /// <param name="value">New authored linear velocity in world units per second.</param>
        public void SetLinearVelocity(float3 value) {
            LinearVelocityValue = value;
        }

        /// <summary>
        /// Returns the authored linear velocity in world units per second.
        /// </summary>
        /// <returns>Current authored linear velocity.</returns>
        public float3 GetLinearVelocity() {
            return LinearVelocityValue;
        }

        /// <summary>
        /// Gets or sets the authored angular velocity in radians per second around each world axis.
        /// </summary>
        public float3 AngularVelocity {
            get { return AngularVelocityValue; }
            set { AngularVelocityValue = value; }
        }

        /// <summary>
        /// Replaces the authored angular velocity with the supplied world-space value.
        /// </summary>
        /// <param name="value">New authored angular velocity in radians per second.</param>
        public void SetAngularVelocity(float3 value) {
            AngularVelocityValue = value;
        }

        /// <summary>
        /// Gets or sets whether gravity should be applied while the body is dynamic.
        /// </summary>
        public bool UseGravity { get; set; }

        /// <summary>
        /// Gets or sets the mass used by dynamic-body resolution.
        /// </summary>
        public double Mass {
            get { return MassValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Mass must be a finite value greater than zero.");
                }

                MassValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the multiplier applied to world gravity for this body.
        /// </summary>
        public double GravityScale {
            get { return GravityScaleValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Gravity scale must be a finite value greater than or equal to zero.");
                }

                GravityScaleValue = value;
            }
        }
    }
}
