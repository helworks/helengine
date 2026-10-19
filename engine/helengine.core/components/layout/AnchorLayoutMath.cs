namespace helengine {
    /// <summary>Shares anchor rectangle calculations between detached cooking and live layout.</summary>
    public static class AnchorLayoutMath {
        /// <summary>Resolves anchored dimensions and position without consulting a scene or retaining authoring values.</summary>
        /// <param name="anchorFlags">Enabled left, right, top and bottom edges.</param>
        /// <param name="distances">Prepared edge distances in left, right, top and bottom order.</param>
        /// <param name="spaceSize">Available anchor bounds.</param>
        /// <param name="spaceOrigin">Origin of those bounds in local coordinates.</param>
        /// <param name="currentSize">Current participant dimensions for single-edge anchors.</param>
        /// <param name="localPosition">Input position, updated on anchored axes only.</param>
        /// <returns>Resolved participant dimensions.</returns>
        public static int2 Resolve(byte anchorFlags, float4 distances, int2 spaceSize, float2 spaceOrigin, int2 currentSize, ref float3 localPosition) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.Anchors(anchorFlags, distances, spaceSize, spaceOrigin, currentSize, ref localPosition, new RuntimeNumericOperations());
        }

        /// <summary>Resolves detached anchors under one explicit build numeric policy.</summary>
        public static int2 Resolve(byte anchorFlags, float4 distances, int2 spaceSize, float2 spaceOrigin, int2 currentSize, ref float3 localPosition, NumericPrecisionPolicy policy) {
            return LayoutNumericKernel<double, CookNumericOperations>.Anchors(anchorFlags, distances, spaceSize, spaceOrigin, currentSize, ref localPosition, new CookNumericOperations(policy));
        }
    }
}
