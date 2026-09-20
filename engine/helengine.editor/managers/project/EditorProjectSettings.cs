namespace helengine.editor {
    /// <summary>
    /// The user-editable identity of a project as shown in the Project Settings dialog: its name and description.
    /// Every other field of the project file is owned by other tools and is carried over untouched on save.
    /// </summary>
    public sealed class EditorProjectSettings {
        /// <summary>
        /// Project name written to the project file and used for the window title and the generated solution.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Free-form project description; blank is stored as absent.
        /// </summary>
        public string Description { get; set; }
    }
}
