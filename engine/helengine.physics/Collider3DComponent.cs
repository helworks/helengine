namespace helengine {
    /// <summary>
    /// Defines the shared authored filtering and trigger settings used by 3D colliders.
    /// </summary>
    public abstract class Collider3DComponent : Component {
        /// <summary>
        /// Backing field for the collision layer bit used by this collider.
        /// </summary>
        ushort CollisionLayerValue;

        /// <summary>
        /// Backing field for the collision mask bits tested against other collider layers.
        /// </summary>
        ushort CollisionMaskValue;

        /// <summary>
        /// Backing field that indicates whether the collider should generate trigger events instead of physical contacts.
        /// </summary>
        bool IsTriggerValue;

        /// <summary>
        /// Backing field for the static friction coefficient applied when contact tangential velocity is small enough to stop completely.
        /// </summary>
        double StaticFrictionValue;

        /// <summary>
        /// Backing field for the dynamic friction coefficient applied when contact tangential velocity should be reduced but not fully cancelled.
        /// </summary>
        double DynamicFrictionValue;

        /// <summary>
        /// Backing field for the restitution coefficient applied to the contact normal impulse.
        /// </summary>
        double RestitutionValue;

        /// <summary>
        /// Initializes one collider with permissive default filtering and solid contact behavior.
        /// </summary>
        protected Collider3DComponent() {
            CollisionLayerValue = 0b0000000000000001;
            CollisionMaskValue = ushort.MaxValue;
            IsTriggerValue = false;
            StaticFrictionValue = 0.6d;
            DynamicFrictionValue = 0.4d;
            RestitutionValue = 0d;
        }

        /// <summary>
        /// Gets or sets the collision layer bit used when other colliders evaluate their masks against this collider.
        /// </summary>
        public ushort CollisionLayer {
            get { return CollisionLayerValue; }
            set {
                if (value == 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Collision layer must contain at least one enabled bit.");
                }

                CollisionLayerValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the collision mask bits used to accept or reject other collider layers.
        /// </summary>
        public ushort CollisionMask {
            get { return CollisionMaskValue; }
            set {
                if (value == 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Collision mask must contain at least one enabled bit.");
                }

                CollisionMaskValue = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this collider should emit trigger overlaps instead of acting as a solid contact.
        /// </summary>
        public bool IsTrigger {
            get { return IsTriggerValue; }
            set { IsTriggerValue = value; }
        }

        /// <summary>
        /// Gets or sets the static friction coefficient used when a contact should stop tangential motion completely.
        /// </summary>
        public double StaticFriction {
            get { return StaticFrictionValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Static friction must be a finite value greater than or equal to zero.");
                }

                StaticFrictionValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the dynamic friction coefficient used when a contact should reduce tangential motion without fully cancelling it.
        /// </summary>
        public double DynamicFriction {
            get { return DynamicFrictionValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Dynamic friction must be a finite value greater than or equal to zero.");
                }

                DynamicFrictionValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the restitution coefficient used to control bounciness on contact normals.
        /// </summary>
        public double Restitution {
            get { return RestitutionValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Restitution must be a finite value between zero and one.");
                }

                RestitutionValue = value;
            }
        }
    }
}
