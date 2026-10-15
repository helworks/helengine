namespace helengine {
    /// <summary>Retains a validated next path sample until the complete input batch can be committed.</summary>
    readonly struct HelPhysicsKinematicPathSample3D {
        /// <summary>Stores the world-space target position for the upcoming fixed step.</summary>
        public readonly float3 Position;
        /// <summary>Stores the displacement velocity used by contacts and moving supports.</summary>
        public readonly float3 Velocity;
        /// <summary>Stores the path clock corresponding to the target position.</summary>
        public readonly double ElapsedSeconds;
        /// <summary>Creates an immutable sample without mutating entities or world commands.</summary>
        public HelPhysicsKinematicPathSample3D(float3 position, float3 velocity, double elapsedSeconds) {
            Position = position;
            Velocity = velocity;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
