namespace helengine.timeline.runtime {
    /// <summary>
    /// Names the nine scalar transform components a cooked transform track can drive. Rotation components are Euler
    /// angles in degrees (X pitch, Y yaw, Z roll). The numeric values are stored in cooked files and must never be
    /// renumbered.
    /// </summary>
    public enum CookedTimelineTransformChannel {
        /// <summary>
        /// Local position X.
        /// </summary>
        PositionX = 0,
        /// <summary>
        /// Local position Y.
        /// </summary>
        PositionY = 1,
        /// <summary>
        /// Local position Z.
        /// </summary>
        PositionZ = 2,
        /// <summary>
        /// Rotation around X in degrees (pitch).
        /// </summary>
        RotationX = 3,
        /// <summary>
        /// Rotation around Y in degrees (yaw).
        /// </summary>
        RotationY = 4,
        /// <summary>
        /// Rotation around Z in degrees (roll).
        /// </summary>
        RotationZ = 5,
        /// <summary>
        /// Local scale X.
        /// </summary>
        ScaleX = 6,
        /// <summary>
        /// Local scale Y.
        /// </summary>
        ScaleY = 7,
        /// <summary>
        /// Local scale Z.
        /// </summary>
        ScaleZ = 8
    }
}
