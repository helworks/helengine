namespace helengine {
    /// <summary>One touch sample in physical window pixels. IDs remain stable until the contact ends or is canceled.</summary>
    public struct InputTouchState {
        /// <summary>Gets or sets the platform contact ID, independent of the pointer's index in a native event.</summary>
        public int Id { get; set; }
        /// <summary>Gets or sets the contact's lifetime transition.</summary>
        public InputTouchPhase Phase { get; set; }
        /// <summary>Gets or sets the horizontal position in physical window pixels.</summary>
        public int X { get; set; }
        /// <summary>Gets or sets the vertical position in physical window pixels.</summary>
        public int Y { get; set; }
        /// <summary>Gets or sets horizontal movement since the preceding sample for this ID.</summary>
        public int DeltaX { get; set; }
        /// <summary>Gets or sets vertical movement since the preceding sample for this ID.</summary>
        public int DeltaY { get; set; }
        /// <summary>Gets or sets nonnegative pressure; native pen values are normalized to zero through one, while finger backends may report values above one.</summary>
        public float Pressure { get; set; }
        /// <summary>Gets or sets the native input source; finger remains the default for existing backends.</summary>
        public InputTouchTool Tool { get; set; }
        /// <summary>Gets or sets monotonic nanoseconds in the platform clock domain; zero means unavailable.</summary>
        public ulong TimestampNanoseconds { get; set; }
        /// <summary>Gets or sets the unrounded horizontal position in physical window pixels.</summary>
        public float PreciseX { get; set; }
        /// <summary>Gets or sets the unrounded vertical position in physical window pixels.</summary>
        public float PreciseY { get; set; }
        /// <summary>Gets or sets axes for which the backend received values, not a device capability claim.</summary>
        public InputPenAxes ReportedPenAxes { get; set; }
        /// <summary>Gets or sets SDL horizontal tilt in degrees, -90 left through +90 right; not UIKit azimuth.</summary>
        public float TiltXDegrees { get; set; }
        /// <summary>Gets or sets SDL vertical tilt in degrees, -90 top through +90 bottom; not UIKit altitude.</summary>
        public float TiltYDegrees { get; set; }
        /// <summary>Gets or sets reported SDL barrel rotation in degrees; consult ReportedPenAxes before use.</summary>
        public float RotationDegrees { get; set; }
        /// <summary>Gets or sets normalized SDL distance above the surface, zero through one, when reported.</summary>
        public float Distance { get; set; }
    }
}
