namespace helengine {
    /// <summary>
    /// Describes one kinetic typography template: the slots an edit fills (items, separator, accent item), the typed
    /// parameters it accepts, the layouts it can arrange items in and the elements it expands into, each with its own
    /// animation timed relative to the moment its item is spoken. Templates carry the motion taste, so edits only choose a
    /// template, fill its slots and anchor each item to a word.
    /// </summary>
    public class GraphicTemplateAsset : Asset, IDisposable {
        /// <summary>
        /// File extension used by authored graphic template assets.
        /// </summary>
        public const string FileExtension = ".hgraphic";

        /// <summary>
        /// Gets or sets the stable template identifier edits reference, such as <c>contrast_chain</c>.
        /// </summary>
        public string TemplateId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable template name shown in editors.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the explanation planners read to decide when the template fits a line.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the template contract version; an edit pins this number so incompatible changes are detected.
        /// </summary>
        public int TemplateVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the seconds the whole graphic takes to fade out before it ends.
        /// </summary>
        public float ExitSeconds { get; set; } = 0.2f;

        /// <summary>
        /// Gets or sets the values an edit fills; exactly one <see cref="GraphicTemplateSlotKind.TextList"/> slot named
        /// <c>items</c> is required.
        /// </summary>
        public GraphicTemplateSlotAsset[] Slots { get; set; } = Array.Empty<GraphicTemplateSlotAsset>();

        /// <summary>
        /// Gets or sets the typed parameters, reusing the effect parameter shapes; their constant slots are unused.
        /// </summary>
        public EffectParameterAsset[] Parameters { get; set; } = Array.Empty<EffectParameterAsset>();

        /// <summary>
        /// Gets or sets the layouts the template supports; automatic layout picks among them by measured fit.
        /// </summary>
        public GraphicTemplateLayoutAsset[] Layouts { get; set; } = Array.Empty<GraphicTemplateLayoutAsset>();

        /// <summary>
        /// Gets or sets the elements every use of the template expands into.
        /// </summary>
        public GraphicTemplateElementAsset[] Elements { get; set; } = Array.Empty<GraphicTemplateElementAsset>();

        /// <summary>
        /// Releases the deserialized definition tree once no compiler borrows it anymore.
        /// </summary>
        public virtual void Dispose() {
            GraphicTemplateSlotAsset[] slots = Slots;
            EffectParameterAsset[] parameters = Parameters;
            GraphicTemplateLayoutAsset[] layouts = Layouts;
            GraphicTemplateElementAsset[] elements = Elements;
            string[] formerIds = FormerAuthoringAssetIds;
            Slots = null;
            Parameters = null;
            Layouts = null;
            Elements = null;
            FormerAuthoringAssetIds = null;
            AnimationClipAsset.DisposeOwnedTracks(slots);
            AnimationClipAsset.DisposeOwnedTracks(parameters);
            AnimationClipAsset.DisposeOwnedTracks(layouts);
            AnimationClipAsset.DisposeOwnedTracks(elements);
            AnimationClipAsset.DeleteOwnedArray(formerIds);
        }
    }
}
