namespace helengine;

public abstract class InputManager {
    public Keyboard Keyboard { get; protected set; }
    public Mouse Mouse { get; protected set; }
    public IInteractable2D Highlighted { get; set; }

    protected Core core;

    private MouseState lastMouseState;
    private MouseState mouseState;

    public InputManager() {
        core = Core.Instance;
    }

    public virtual void Update() {
        List<IInteractable2D> interactables = core.ObjectManager.Interactables;

        lastMouseState = mouseState;
        mouseState = core.InputManager.Mouse.GetState();

        PointerInteraction interaction = PointerInteraction.None;
        if (mouseState.LeftButton == ButtonState.Released &&
            lastMouseState.LeftButton == ButtonState.Pressed) {
            interaction = PointerInteraction.Release;
        } else if (mouseState.LeftButton == ButtonState.Pressed &&
            lastMouseState.LeftButton == ButtonState.Released) {
            interaction = PointerInteraction.Press;
        }

        for (int i = 0; i < interactables.Count; i++) {
            IInteractable2D interactable = interactables[i];

            float3 pos = interactable.Parent.Position;
            int2 size = interactable.Size;

            float4 rect = new float4(pos.X, pos.Y, size.X, size.Y);

            if (rect.Contains(mouseState.X, mouseState.Y)) {
                if (interaction == PointerInteraction.Press) {
                    Highlighted = interactable;
                }
            }
        }

        if (Highlighted != null) {
            int deltaX = mouseState.X - lastMouseState.X;
            int deltaY = mouseState.Y - lastMouseState.Y;
            if (interaction == PointerInteraction.None &&
                (Math.Abs(deltaX) > 0 ||
                Math.Abs(deltaY) > 0)
                ) {
                interaction = PointerInteraction.Hover;
            }

            Highlighted.OnCursor(new int2(mouseState.X, mouseState.Y), new int2(deltaX, deltaY), interaction);

            if (interaction == PointerInteraction.Release) {
                Highlighted = null;
            }
        }
    }
}
