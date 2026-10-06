namespace helengine.editor {
    /// <summary>Initializes scrollbar visuals and dragging for editor UI independently of scene runtime scrollbars.</summary>
    [RunInEditor]
    public sealed class EditorScrollBarComponent : ScrollBarComponent {
        /// <summary>Creates an editor scrollbar with positive initial track bounds.</summary>
        /// <param name="size">Full track width and height in pixels.</param>
        public EditorScrollBarComponent(int2 size) : base(size) {
        }
    }
}
