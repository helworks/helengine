namespace helengine {
    /// <summary>Identifies a low level trigger overlap transition.</summary>
    public enum HelPhysicsTriggerEventKind3D : byte {
        /// <summary>The overlap was first observed.</summary>
        Enter = 0,

        /// <summary>The overlap remained continuously observed.</summary>
        Stay = 1,

        /// <summary>The prior overlap ended.</summary>
        Exit = 2
    }
}
