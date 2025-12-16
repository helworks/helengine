namespace helengine;

/// <summary>
/// Routes mouse input to interactable components and tracks hover/press state.
/// </summary>
public abstract class InputManager {
    /// <summary>
    /// Gets the keyboard input device.
    /// </summary>
    public Keyboard Keyboard { get; protected set; }

    /// <summary>
    /// Gets the mouse input device.
    /// </summary>
    public Mouse Mouse { get; protected set; }

    /// <summary>
    /// Gets the interactable currently captured by a press.
    /// </summary>
    public IInteractable2D? Highlighted { get; private set; }

    /// <summary>
    /// Gets the interactable currently hovered by the pointer.
    /// </summary>
    public IInteractable2D? Hovering { get; private set; }

    protected Core core;

    ICamera? capturedCamera; // Camera that captured the pointer on press
    MouseState lastMouseState;
    MouseState mouseState;

    /// <summary>
    /// Initializes the input manager and caches the core instance.
    /// </summary>
    public InputManager() {
        core = Core.Instance;
    }

    /// <summary>
    /// Processes mouse state, performs hit tests, and dispatches pointer events.
    /// </summary>
    public virtual void Update() {
        var objectManager = core.ObjectManager;
        List<IInteractable2D> interactables = objectManager.Interactables;

        lastMouseState = mouseState;
        mouseState = Mouse.GetState();

        PointerInteraction interaction = PointerInteraction.None;
        if (mouseState.LeftButton == ButtonState.Released &&
            lastMouseState.LeftButton == ButtonState.Pressed) {
            interaction = PointerInteraction.Release;
        } else if (mouseState.LeftButton == ButtonState.Pressed &&
            lastMouseState.LeftButton == ButtonState.Released) {
            interaction = PointerInteraction.Press;
        }

        // Determine the topmost camera under the cursor
        ICamera? topCamera = GetTopmostCameraAt(mouseState.X, mouseState.Y);

        // Compute local mouse coordinates within that camera's viewport
        float2 localMouse = new float2(mouseState.X, mouseState.Y);
        if (topCamera != null) {
            float4 vp = topCamera.Viewport;
            localMouse.X -= vp.X;
            localMouse.Y -= vp.Y;
        }

        // If we have a captured interactable (pressed), send events to it regardless
        if (Highlighted != null) {
            // Use the camera captured at press time for relative coords
            float2 capturedLocal = new float2(mouseState.X, mouseState.Y);
            if (capturedCamera != null) {
                float4 cvp = capturedCamera.Viewport;
                capturedLocal.X -= cvp.X;
                capturedLocal.Y -= cvp.Y;
            }

            int2 rel = new int2(
                (int)MathF.Round(capturedLocal.X - Highlighted.Parent.Position.X),
                (int)MathF.Round(capturedLocal.Y - Highlighted.Parent.Position.Y)
            );

            int deltaX = mouseState.X - lastMouseState.X;
            int deltaY = mouseState.Y - lastMouseState.Y;
            if (interaction == PointerInteraction.None && (deltaX != 0 || deltaY != 0)) {
                interaction = PointerInteraction.Hover;
            }

            Highlighted.OnCursor(rel, new int2(deltaX, deltaY), interaction);

            if (interaction == PointerInteraction.Release) {
                Highlighted = null;
                capturedCamera = null;
            }

            return; // Captured; do not process hover routing
        }

        // No captured interactable; hit test within the topmost camera
        IInteractable2D? hit = null;
        if (topCamera != null) {
            ushort camMask = topCamera.LayerMask;
            for (int i = 0; i < interactables.Count; i++) {
                IInteractable2D interactable = interactables[i];

                // Filter by camera layer mask
                if ((interactable.Parent.LayerMask & camMask) == 0) continue;

                float3 pos = interactable.Parent.Position;
                int2 size = interactable.Size;
                float4 rect = new float4(pos.X, pos.Y, size.X, size.Y);

                if (rect.Contains(localMouse.X, localMouse.Y)) {
                    hit = interactable;
                    // We do not break to allow later interactables to override earlier ones if overlapping
                }
            }
        }

        // Handle hover/leave transitions and press
        if (hit != Hovering) {
            if (Hovering != null) {
                // Compute relative coords for the previous hovered for a clean leave event
                float2 prevLocal = new float2(mouseState.X, mouseState.Y);
                ICamera? prevCam = FindCameraForInteractableAt(Hovering, mouseState.X, mouseState.Y);
                if (prevCam != null) {
                    float4 vp = prevCam.Viewport;
                    prevLocal.X -= vp.X;
                    prevLocal.Y -= vp.Y;
                }
                int2 prevRel = new int2(
                    (int)MathF.Round(prevLocal.X - Hovering.Parent.Position.X),
                    (int)MathF.Round(prevLocal.Y - Hovering.Parent.Position.Y)
                );
                Hovering.OnCursor(prevRel, new int2(0, 0), PointerInteraction.Leave);
            }
            Hovering = hit;
        }

        if (Hovering != null) {
            // Calculate relative position within the hit interactable
            int2 rel = new int2(
                (int)MathF.Round(localMouse.X - Hovering.Parent.Position.X),
                (int)MathF.Round(localMouse.Y - Hovering.Parent.Position.Y)
            );

            int deltaX = mouseState.X - lastMouseState.X;
            int deltaY = mouseState.Y - lastMouseState.Y;

            // Click started on this interactable
            if (interaction == PointerInteraction.Press) {
                Highlighted = Hovering;
                capturedCamera = topCamera;
                Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Press);
            } else {
                // Hover movement
                if (deltaX != 0 || deltaY != 0) {
                    Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Hover);
                }
            }
        } else {
            // Not over any interactable
            if (Hovering != null) {
                Hovering.OnCursor(new int2(0, 0), new int2(0, 0), PointerInteraction.Leave);
                Hovering = null;
            }
        }
    }

    /// <summary>
    /// Finds the topmost camera whose viewport contains the given screen coordinates.
    /// </summary>
    private ICamera? GetTopmostCameraAt(int x, int y) {
        var cameraBuckets = core.ObjectManager.Cameras;
        for (int b = cameraBuckets.Length - 1; b >= 0; b--) {
            var list = cameraBuckets[b];
            for (int i = list.Count - 1; i >= 0; i--) {
                var cam = list[i];
                var vp = cam.Viewport;
                if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W) {
                    return cam;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the camera containing the specified interactable at the given mouse position.
    /// </summary>
    private ICamera? FindCameraForInteractableAt(IInteractable2D interactable, int mouseX, int mouseY) {
        var cameraBuckets = core.ObjectManager.Cameras;
        for (int b = cameraBuckets.Length - 1; b >= 0; b--) {
            var list = cameraBuckets[b];
            for (int i = list.Count - 1; i >= 0; i--) {
                var cam = list[i];
                if ((interactable.Parent.LayerMask & cam.LayerMask) == 0) continue;
                var vp = cam.Viewport;
                if (mouseX >= vp.X && mouseX < vp.X + vp.Z && mouseY >= vp.Y && mouseY < vp.Y + vp.W) {
                    return cam;
                }
            }
        }
        return null;
    }
}
