namespace helengine.editor {
    /// <summary>Runs scrolling and scrollbar creation for editor-owned UI while scene scroll components remain inactive during authoring.</summary>
    [RunInEditor]
    public sealed class EditorScrollComponent : ScrollComponent {
        /// <summary>Creates an editor-owned scrollbar that can initialize its visuals and pointer input during authoring.</summary>
        /// <param name="size">Initial positive track bounds before viewport layout is applied.</param>
        /// <returns>Scrollbar explicitly opted into editor lifecycle execution.</returns>
        protected override ScrollBarComponent CreateScrollBarComponent(int2 size) {
            return new EditorScrollBarComponent(size);
        }
    }
}
