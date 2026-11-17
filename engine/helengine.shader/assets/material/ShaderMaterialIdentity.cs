namespace helengine {
    /// <summary>Allocates material lifetime identities without retaining material objects or reusing disposed native addresses.</summary>
    static class ShaderMaterialIdentity {
        /// <summary>Last allocated identity, accessed atomically because assets may be constructed on worker threads.</summary>
        static long LastIdentity;

        /// <summary>Returns one positive unique identity, refusing exhaustion before the counter can wrap.</summary>
        public static long Next() {
            while (true) {
                long current = System.Threading.Interlocked.CompareExchange(ref LastIdentity, 0L, 0L);
                if (current == long.MaxValue) {
                    throw new OverflowException("Shader material lifetime identities exhausted.");
                }
                long next = current + 1;
                if (System.Threading.Interlocked.CompareExchange(ref LastIdentity, next, current) == current) {
                    return next;
                }
            }
        }
    }
}
