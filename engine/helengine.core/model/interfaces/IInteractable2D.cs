namespace helengine {
    public interface IInteractable2D {
        Entity Parent { get; }

        int2 Size { get; set; }

        void OnCursor(int2 relPos, int2 delta, PointerInteraction state);
    }
}
