namespace helengine.editor.windows.importerprobe {
    /// <summary>
    /// Captures importer backend assembly load state reported by the isolated probe process.
    /// </summary>
    public sealed class ImporterProbeResult {
        /// <summary>
        /// Gets or sets whether the GDI importer backend assembly was loaded before host registration.
        /// </summary>
        public bool GdiLoadedBeforeRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the GDI importer backend assembly was loaded immediately after host registration.
        /// </summary>
        public bool GdiLoadedAfterRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the GDI importer backend assembly was loaded after the first GDI import.
        /// </summary>
        public bool GdiLoadedAfterImport { get; set; }

        /// <summary>
        /// Gets or sets whether the Pfim importer backend assembly was loaded before host registration.
        /// </summary>
        public bool PfimLoadedBeforeRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Pfim importer backend assembly was loaded immediately after host registration.
        /// </summary>
        public bool PfimLoadedAfterRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Pfim importer backend assembly was loaded after the first Pfim import.
        /// </summary>
        public bool PfimLoadedAfterImport { get; set; }

        /// <summary>
        /// Gets or sets whether the Magick importer backend assembly was loaded before host registration.
        /// </summary>
        public bool MagickLoadedBeforeRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Magick importer backend assembly was loaded immediately after host registration.
        /// </summary>
        public bool MagickLoadedAfterRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Magick importer backend assembly was loaded after the first Magick import.
        /// </summary>
        public bool MagickLoadedAfterImport { get; set; }

        /// <summary>
        /// Gets or sets whether the Assimp importer backend assembly was loaded before host registration.
        /// </summary>
        public bool AssimpLoadedBeforeRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Assimp importer backend assembly was loaded immediately after host registration.
        /// </summary>
        public bool AssimpLoadedAfterRegistration { get; set; }

        /// <summary>
        /// Gets or sets whether the Assimp importer backend assembly was loaded after the first model import.
        /// </summary>
        public bool AssimpLoadedAfterImport { get; set; }
    }
}
