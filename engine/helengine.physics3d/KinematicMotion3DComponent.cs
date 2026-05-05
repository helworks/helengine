namespace helengine {
    /// <summary>
    /// Stores one authored kinematic motion path consumed by the 3D physics runtime.
    /// </summary>
    public sealed class KinematicMotion3DComponent : Component {
        /// <summary>
        /// Backing field for the motion path start position.
        /// </summary>
        float3 StartLocalPositionValue;

        /// <summary>
        /// Backing field for the motion path end position.
        /// </summary>
        float3 EndLocalPositionValue;

        /// <summary>
        /// Backing field for the one-way travel duration in seconds.
        /// </summary>
        double TravelDurationSecondsValue;

        /// <summary>
        /// Initializes a new kinematic motion path with simple horizontal defaults.
        /// </summary>
        public KinematicMotion3DComponent() {
            StartLocalPositionValue = float3.Zero;
            EndLocalPositionValue = new float3(1f, 0f, 0f);
            TravelDurationSecondsValue = 1d;
            PingPong = true;
        }

        /// <summary>
        /// Gets or sets the local-space start position used by the motion path.
        /// </summary>
        public float3 StartLocalPosition {
            get { return StartLocalPositionValue; }
            set { StartLocalPositionValue = value; }
        }

        /// <summary>
        /// Gets or sets the local-space end position used by the motion path.
        /// </summary>
        public float3 EndLocalPosition {
            get { return EndLocalPositionValue; }
            set { EndLocalPositionValue = value; }
        }

        /// <summary>
        /// Gets or sets the one-way travel duration in seconds.
        /// </summary>
        public double TravelDurationSeconds {
            get { return TravelDurationSecondsValue; }
            set {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Travel duration must be a finite value greater than zero.");
                }

                TravelDurationSecondsValue = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the path should reverse at the end instead of clamping there.
        /// </summary>
        public bool PingPong { get; set; }
    }
}
