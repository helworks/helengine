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
            int targetWidth = currentSize.X;
            int targetHeight = currentSize.Y;

            bool hasLeft = (anchorFlags & LayoutComponent.LeftAnchorFlag) != 0;
            bool hasRight = (anchorFlags & LayoutComponent.RightAnchorFlag) != 0;
            bool hasTop = (anchorFlags & LayoutComponent.TopAnchorFlag) != 0;
            bool hasBottom = (anchorFlags & LayoutComponent.BottomAnchorFlag) != 0;

            if (hasLeft && hasRight) {
                targetWidth = Math.Max(0, (int)Math.Round(spaceSize.X - distances.X - distances.Y));
                localPosition.X = spaceOrigin.X + distances.X;
            } else if (hasLeft) {
                localPosition.X = spaceOrigin.X + distances.X;
            } else if (hasRight) {
                localPosition.X = spaceOrigin.X + spaceSize.X - distances.Y - currentSize.X;
            }

            if (hasTop && hasBottom) {
                targetHeight = Math.Max(0, (int)Math.Round(spaceSize.Y - distances.Z - distances.W));
                localPosition.Y = spaceOrigin.Y + distances.Z;
            } else if (hasTop) {
                localPosition.Y = spaceOrigin.Y + distances.Z;
            } else if (hasBottom) {
                localPosition.Y = spaceOrigin.Y + spaceSize.Y - distances.W - currentSize.Y;
            }

            return new int2(targetWidth, targetHeight);
        }
    }
}
