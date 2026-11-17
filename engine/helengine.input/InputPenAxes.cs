namespace helengine {
    /// <summary>Bit values identifying received axes; zero-valued SDL axes do not prove hardware support.</summary>
    public enum InputPenAxes {
        /// <summary>No axis values have been received.</summary>
        None = 0,
        /// <summary>Normalized tip pressure has been received.</summary>
        Pressure = 1,
        /// <summary>Horizontal tilt in SDL degrees has been received.</summary>
        TiltX = 2,
        /// <summary>Vertical tilt in SDL degrees has been received.</summary>
        TiltY = 4,
        /// <summary>Barrel rotation in SDL degrees has been received.</summary>
        Rotation = 8,
        /// <summary>Normalized distance above the surface has been received.</summary>
        Distance = 16
    }
}
