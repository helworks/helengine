namespace helengine.editor {
    /// <summary>
    /// Which section of the Play settings a value belongs to.
    /// </summary>
    public enum EditorPlaySettingScope {
        /// <summary>A common Play setting the engine declares for every platform.</summary>
        Common,

        /// <summary>A Play setting the selected platform declares on its Play capability.</summary>
        Platform
    }
}
