namespace helengine {
    /// <summary>
    /// Stores the authored locomotion settings consumed by 3D character-controller runtimes.
    /// </summary>
    public sealed class CharacterController3DComponent : Component {
        /// <summary>
        /// Backing field for the authored desired move direction.
        /// </summary>
        float3 DesiredMoveDirectionValue;

        /// <summary>
        /// Backing field for the authored move speed.
        /// </summary>
        double MoveSpeedValue;

        /// <summary>
        /// Backing field for the authored gravity scale.
        /// </summary>
        double GravityScaleValue;

        /// <summary>
        /// Backing field for the authored step height.
        /// </summary>
        double StepHeightValue;

        /// <summary>
        /// Backing field for the authored ground snap distance.
        /// </summary>
        double GroundSnapDistanceValue;

        /// <summary>
        /// Backing field for the authored maximum walkable slope angle in degrees.
        /// </summary>
        double MaximumSlopeDegreesValue;

        /// <summary>
        /// Initializes a new character controller with conservative walk defaults.
        /// </summary>
        public CharacterController3DComponent() {
            DesiredMoveDirectionValue = float3.Zero;
            MoveSpeedValue = 0d;
            GravityScaleValue = 1d;
            StepHeightValue = 0.5d;
            GroundSnapDistanceValue = 0.25d;
            MaximumSlopeDegreesValue = 45d;
        }

        /// <summary>
        /// Gets or sets the desired planar move direction requested by gameplay.
        /// </summary>
        public float3 DesiredMoveDirection {
            get { return DesiredMoveDirectionValue; }
            set { DesiredMoveDirectionValue = value; }
        }

        /// <summary>
        /// Gets or sets the horizontal move speed in world units per second.
        /// </summary>
        public double MoveSpeed {
            get { return MoveSpeedValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Move speed must be a finite value greater than or equal to zero.");
                }

                MoveSpeedValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the multiplier applied to gravity while the controller is airborne.
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

        /// <summary>
        /// Gets or sets the maximum upward support change that can be climbed in one step.
        /// </summary>
        public double StepHeight {
            get { return StepHeightValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Step height must be a finite value greater than or equal to zero.");
                }

                StepHeightValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum downward snap distance used to keep the controller grounded.
        /// </summary>
        public double GroundSnapDistance {
            get { return GroundSnapDistanceValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Ground snap distance must be a finite value greater than or equal to zero.");
                }

                GroundSnapDistanceValue = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum walkable slope angle in degrees used when evaluating support surfaces beneath the controller.
        /// </summary>
        public double MaximumSlopeDegrees {
            get { return MaximumSlopeDegreesValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value >= 90d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Maximum slope degrees must be a finite value greater than or equal to zero and less than ninety.");
                }

                MaximumSlopeDegreesValue = value;
            }
        }
    }
}
