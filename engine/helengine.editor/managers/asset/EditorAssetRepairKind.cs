namespace helengine.editor {
    /// <summary>
    /// Identifies one automatic authored-asset repair operation.
    /// </summary>
    public enum EditorAssetRepairKind {
        /// <summary>An external identity document was created for an authored source.</summary>
        MissingExternalMetadataCreation,

        /// <summary>A saved reference identity was adopted by its saved path.</summary>
        SavedIdAdoption,

        /// <summary>A copied identity was reassigned to a non-owning asset.</summary>
        DuplicateIdReassignment,

        /// <summary>A saved reference path was updated to the selected asset path.</summary>
        PathHealing,

        /// <summary>A saved reference content hash was updated to the selected asset hash.</summary>
        HashHealing,

        /// <summary>A saved reference was refreshed to its complete canonical representation.</summary>
        CanonicalReferenceRefresh
    }
}
