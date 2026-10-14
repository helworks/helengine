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
        /// <summary>Gets or sets nonnegative device-reported pressure; devices may report values above one.</summary>
        public float Pressure { get; set; }
    }
}
