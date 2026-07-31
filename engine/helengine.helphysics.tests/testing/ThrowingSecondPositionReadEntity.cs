namespace helengine {
    /// <summary>
    /// Provides a deterministic entity fixture that succeeds during physics preflight and fails during commit-time pose translation.
    /// </summary>
    sealed class ThrowingSecondPositionReadEntity : Entity {
        /// <summary>
        /// Stores how many world-position reads have occurred across binder validation and commit passes.
        /// </summary>
        int PositionReadCount;

        /// <summary>
        /// Gets the base world position once, then throws on later reads to exercise binding transaction rollback.
        /// </summary>
        public override float3 Position {
            get {
                PositionReadCount++;
                if (PositionReadCount > 1) {
                    throw new InvalidOperationException("The test entity rejects its commit-time position read.");
                }

                return base.Position;
            }
            set {
                base.Position = value;
            }
        }
    }
}
