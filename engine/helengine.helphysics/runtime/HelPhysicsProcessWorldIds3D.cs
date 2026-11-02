namespace helengine {
    /// <summary>Owns the process-wide monotonic world identity sequence independently of scene simulation modules.</summary>
    public static class HelPhysicsProcessWorldIds3D {
        /// <summary>Retains the one allocator whose exhaustion and assigned IDs survive scene module reloads.</summary>
        static readonly HelPhysicsWorldIdAllocator3D Allocator = new HelPhysicsWorldIdAllocator3D();

        /// <summary>Validates process identity storage without consuming an ownership token.</summary>
        public static void PrepareProcessState() {
            if (Allocator == null) { throw new InvalidOperationException("Process world identity storage is missing."); }
        }

        /// <summary>Returns the next process-local world token or preserves permanent exhaustion.</summary>
        internal static uint Allocate() {
            PrepareProcessState();
            return Allocator.Allocate();
        }
    }
}
