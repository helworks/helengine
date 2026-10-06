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
        /// <returns>A list-owned interactable borrowed by the input router, or null when nothing matches.</returns>
        [NativeBorrowedReturn]
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
            int hitInteractableIndex = -1;

            for (int interactableIndex = 0; interactableIndex < interactables.Count; interactableIndex++) {
                IInteractable2D interactable = interactables[interactableIndex];
                if (interactable == null || interactable.Parent == null || !interactable.Parent.IsHierarchyEnabled || (interactable.Parent.LayerMask & cameraLayerMask) == 0) {
                    continue;
                }

                ICameraBoundViewportOwner viewportOwner = FindNearestViewportOwner(interactable.Parent);
                if (viewportOwner != null) {
                    CameraComponent boundCamera = viewportOwner.GetBoundCameraComponent();
                    if ((viewportOwner.BindingMode == ViewportComponent.ExplicitCameraBindingMode ||
                         (viewportOwner.BindingMode == ViewportComponent.AncestorCameraBindingMode && boundCamera != null)) &&
                        !ReferenceEquals(boundCamera, camera)) {
                        continue;
                    }
                }

                ResolvePointerInInteractableSpace(interactable, camera, pointerX, pointerY, out int localPointerX, out int localPointerY);
                if (!IsInsideActiveClipRegions(interactable, localPointerX, localPointerY)) {
                    continue;
                }

                float3 position = interactable.Parent.Position;
                float4 rect = new float4(position.X, position.Y, interactable.Size.X, interactable.Size.Y);
                if (!rect.Contains(localPointerX, localPointerY)) {
                    continue;
                }

                int comparison = hit == null ? 1 : RenderDepthOrder2D.CompareEntities(interactable.Parent, hit.Parent);
                if (comparison == 0 && hit != null && ReferenceEquals(interactable.Parent, hit.Parent)
                    && interactable is Component candidateComponent && hit is Component hitComponent) {
                    comparison = interactable.Parent.Components.IndexOf(candidateComponent).CompareTo(interactable.Parent.Components.IndexOf(hitComponent));
                }
                if (hit == null || comparison > 0 || (comparison == 0 && interactableIndex > hitInteractableIndex)) {
                    hit = interactable;
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

            ResolvePointerInInteractableSpace(interactable, camera, pointerX, pointerY, out int localPointerX, out int localPointerY);
            float3 position = interactable.Parent.Position;
            relativeX = (int)Math.Round(localPointerX - position.X);
            relativeY = (int)Math.Round(localPointerY - position.Y);
        }

        /// <summary>
        /// Normalizes one window-space pointer into the local coordinate space used by one interactable subtree.
        /// </summary>
        /// <param name="interactable">Interactable whose viewport ownership should be considered.</param>
        /// <param name="camera">Camera currently routing the pointer.</param>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <param name="resolvedPointerX">Receives the pointer X coordinate in the interactable subtree space.</param>
        /// <param name="resolvedPointerY">Receives the pointer Y coordinate in the interactable subtree space.</param>
        static void ResolvePointerInInteractableSpace(
            IInteractable2D interactable,
            ICamera camera,
            int pointerX,
            int pointerY,
            out int resolvedPointerX,
            out int resolvedPointerY) {
            resolvedPointerX = pointerX;
            resolvedPointerY = pointerY;
            if (interactable == null || interactable.Parent == null || camera == null) {
                return;
            }

            ICameraBoundViewportOwner viewportOwner = FindNearestViewportOwner(interactable.Parent);
            if (viewportOwner == null) {
                return;
            }

            CameraComponent boundCamera = viewportOwner.GetBoundCameraComponent();
            if (!ReferenceEquals(boundCamera, camera)) {
                return;
            }

            float4 viewportBounds = viewportOwner.ResolvedViewportBounds;
            resolvedPointerX -= (int)Math.Round(viewportBounds.X);
            resolvedPointerY -= (int)Math.Round(viewportBounds.Y);
        }

        /// <summary>
        /// Resolves the nearest viewport owner governing one interactable subtree.
        /// </summary>
        /// <param name="entity">Interactable owner whose ancestors should be inspected.</param>
        /// <returns>An entity-owned viewport owner borrowed for pointer routing, or null when none applies.</returns>
        [NativeBorrowedReturn]
        static ICameraBoundViewportOwner FindNearestViewportOwner(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                        if (current.Components[componentIndex] is ICameraBoundViewportOwner viewportOwner) {
                            return viewportOwner;
                        }
                    }
                }

                current = current.Parent;
            }

            return null;
        }

    }
}
