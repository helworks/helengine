namespace helengine {
    /// <summary>
    /// Resolves the top-most 2D interactable for one pointer position and camera.
    /// </summary>
    public static class PointerInteractableHitResolver {
        /// <summary>
        /// Resolves the top-most interactable under one pointer position for one camera.
        /// </summary>
        /// <param name="interactables">Registered interactables considered for the hit test.</param>
        /// <param name="drawables2D">Registered drawables used to evaluate visual order.</param>
        /// <param name="camera">Camera whose viewport and layer mask scope the hit test.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <returns>Top-most interactable under the pointer, or null when nothing matches.</returns>
        public static IInteractable2D ResolveTopInteractableAt(
            List<IInteractable2D> interactables,
            List<IDrawable2D> drawables2D,
            ICamera camera,
            int pointerX,
            int pointerY) {
            if (interactables == null) {
                throw new ArgumentNullException(nameof(interactables));
            }
            if (drawables2D == null) {
                throw new ArgumentNullException(nameof(drawables2D));
            }
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            ushort cameraLayerMask = camera.LayerMask;
            IInteractable2D hit = null;
            byte hitRenderOrder = 0;
            int hitDrawableIndex = -1;
            int hitInteractableIndex = -1;

            for (int interactableIndex = 0; interactableIndex < interactables.Count; interactableIndex++) {
                IInteractable2D interactable = interactables[interactableIndex];
                if ((interactable.Parent.LayerMask & cameraLayerMask) == 0) {
                    continue;
                }

                if (!IsInsideActiveClipRegions(interactable, pointerX, pointerY)) {
                    continue;
                }

                float3 position = interactable.Parent.Position;
                float4 rect = new float4(position.X, position.Y, interactable.Size.X, interactable.Size.Y);
                if (!rect.Contains(pointerX, pointerY)) {
                    continue;
                }

                byte candidateRenderOrder = GetTopDrawableRenderOrder(drawables2D, interactable, cameraLayerMask, out int candidateDrawableIndex);
                if (hit == null ||
                    CandidateIsInFront(candidateRenderOrder, candidateDrawableIndex, interactableIndex, hitRenderOrder, hitDrawableIndex, hitInteractableIndex)) {
                    hit = interactable;
                    hitRenderOrder = candidateRenderOrder;
                    hitDrawableIndex = candidateDrawableIndex;
                    hitInteractableIndex = interactableIndex;
                }
            }

            return hit;
        }

        /// <summary>
        /// Determines whether one pointer position lies inside every active clip region that constrains an interactable.
        /// </summary>
        /// <param name="interactable">Interactable whose ancestor clip regions should be checked.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <returns>True when the pointer is inside every clip region, or when no clip regions are present.</returns>
        static bool IsInsideActiveClipRegions(IInteractable2D interactable, int pointerX, int pointerY) {
            if (interactable == null || interactable.Parent == null) {
                return false;
            }

            Entity current = interactable.Parent;
            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                        if (current.Components[componentIndex] is IClipRegion2D clipRegion) {
                            float4 clipRect = clipRegion.GetClipRect();
                            if (!GeometryUtils.IsPointInsideRect(pointerX, pointerY, new float3(clipRect.X, clipRect.Y, 0f), (int)clipRect.Z, (int)clipRect.W)) {
                                return false;
                            }
                        }
                    }
                }

                current = current.Parent;
            }

            return true;
        }

        /// <summary>
        /// Converts one window-space pointer position into coordinates relative to one interactable.
        /// </summary>
        /// <param name="interactable">Interactable receiving the pointer.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <param name="camera">Camera whose viewport should be subtracted first.</param>
        /// <param name="relativeX">Receives the pointer X coordinate relative to the interactable.</param>
        /// <param name="relativeY">Receives the pointer Y coordinate relative to the interactable.</param>
        public static void GetRelativePointerForInteractable(
            IInteractable2D interactable,
            int pointerX,
            int pointerY,
            ICamera camera,
            out int relativeX,
            out int relativeY) {
            if (interactable == null) {
                throw new ArgumentNullException(nameof(interactable));
            }

            float3 position = interactable.Parent.Position;
            relativeX = (int)Math.Round(pointerX - position.X);
            relativeY = (int)Math.Round(pointerY - position.Y);
        }

        /// <summary>
        /// Chooses the strongest drawable render order associated with one interactable.
        /// </summary>
        /// <param name="drawables2D">Registered 2D drawables.</param>
        /// <param name="interactable">Interactable being evaluated.</param>
        /// <param name="cameraLayerMask">Layer mask rendered by the active camera.</param>
        /// <param name="candidateDrawableIndex">Receives the drawable index used for tie-breaking.</param>
        /// <returns>Highest drawable render order associated with the interactable.</returns>
        static byte GetTopDrawableRenderOrder(
            List<IDrawable2D> drawables2D,
            IInteractable2D interactable,
            ushort cameraLayerMask,
            out int candidateDrawableIndex) {
            candidateDrawableIndex = -1;
            byte renderOrder = 0;
            if (drawables2D == null || interactable == null) {
                return renderOrder;
            }

            for (int drawableIndex = 0; drawableIndex < drawables2D.Count; drawableIndex++) {
                IDrawable2D drawable = drawables2D[drawableIndex];
                if (drawable.Parent != interactable.Parent) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & cameraLayerMask) == 0) {
                    continue;
                }

                if (candidateDrawableIndex < 0 || drawable.RenderOrder2D >= renderOrder) {
                    renderOrder = drawable.RenderOrder2D;
                    candidateDrawableIndex = drawableIndex;
                }
            }

            return renderOrder;
        }

        /// <summary>
        /// Determines whether one candidate should replace the current winning hit.
        /// </summary>
        /// <param name="candidateRenderOrder">Candidate render order.</param>
        /// <param name="candidateDrawableIndex">Candidate drawable index.</param>
        /// <param name="candidateInteractableIndex">Candidate interactable registration index.</param>
        /// <param name="currentRenderOrder">Current winning render order.</param>
        /// <param name="currentDrawableIndex">Current winning drawable index.</param>
        /// <param name="currentInteractableIndex">Current winning interactable registration index.</param>
        /// <returns>True when the candidate is visually in front of the current winner.</returns>
        static bool CandidateIsInFront(
            byte candidateRenderOrder,
            int candidateDrawableIndex,
            int candidateInteractableIndex,
            byte currentRenderOrder,
            int currentDrawableIndex,
            int currentInteractableIndex) {
            if (candidateRenderOrder != currentRenderOrder) {
                return candidateRenderOrder > currentRenderOrder;
            }

            if (candidateDrawableIndex != currentDrawableIndex) {
                return candidateDrawableIndex > currentDrawableIndex;
            }

            return candidateInteractableIndex > currentInteractableIndex;
        }
    }
}
