namespace helengine {
    /// <summary>
    /// Defines fixed body and shape slot counts that a physics world allocates before simulation begins.
    /// </summary>
    public readonly struct HelPhysicsWorldCapacity3D {
        /// <summary>
        /// Stores the fixed number of body slots available to the world.
        /// </summary>
        public readonly int BodyCapacity;

        /// <summary>
        /// Stores the fixed number of shape slots available to the world.
        /// </summary>
        public readonly int ShapeCapacity;

        /// <summary>
        /// Initializes fixed body and shape capacities for a world.
        /// </summary>
        /// <param name="bodyCapacity">Number of addressable body slots.</param>
        /// <param name="shapeCapacity">Number of addressable shape slots.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either capacity cannot be represented by a valid handle index.</exception>
        public HelPhysicsWorldCapacity3D(int bodyCapacity, int shapeCapacity) {
            ValidateCapacity(bodyCapacity, nameof(bodyCapacity));
            ValidateCapacity(shapeCapacity, nameof(shapeCapacity));

            BodyCapacity = bodyCapacity;
            ShapeCapacity = shapeCapacity;
        }

        /// <summary>
        /// Validates a fixed pool capacity against the representable body and shape handle range.
        /// </summary>
        /// <param name="capacity">Requested number of addressable slots.</param>
        /// <param name="parameterName">Name of the capacity parameter being validated.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is outside the supported range.</exception>
        static void ValidateCapacity(int capacity, string parameterName) {
            if (capacity < 1 || capacity > 65534) {
                throw new ArgumentOutOfRangeException(parameterName, "Physics pool capacities must be between 1 and 65,534 inclusive.");
            }
        }
    }
}
