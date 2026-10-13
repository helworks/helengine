namespace helengine.editor {
    /// <summary>
    /// Describes where a generated module project writes its compiled assembly beneath its base output path. The
    /// generated project file and the reported <see cref="EditorGeneratedCodeModuleProject.OutputDirectoryPath"/> are
    /// both derived from the same layout, so consumers can trust the reported directory.
    /// </summary>
    public enum EditorGeneratedCodeOutputLayout {
        /// <summary>
        /// The SDK default: <c>&lt;base output&gt;/&lt;Configuration&gt;/&lt;TargetFramework&gt;</c>. Used by editor hot
        /// reload, headless editor commands and console platform builds.
        /// </summary>
        ConfigurationAndTargetFramework,

        /// <summary>
        /// Flat per-module folder: the assembly is written directly into <c>&lt;base output&gt;</c>, which is
        /// <c>generated_code/bin/&lt;ModuleId&gt;</c>. This is the layout the player loads Play script modules from.
        /// </summary>
        ModuleFolder
    }
}
