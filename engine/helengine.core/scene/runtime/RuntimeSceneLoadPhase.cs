namespace helengine {
    /// <summary>Orders cooperative scene preparation so no hierarchy becomes active before every readiness phase completes.</summary>
    internal enum RuntimeSceneLoadPhase {
        /// <summary>Creates entity shells and deserializes individual component records.</summary>
        Materialize = 0,
        /// <summary>Prepares individual mesh components before initialization.</summary>
        PrepareMeshes = 1,
        /// <summary>Binds references against individual authored entities.</summary>
        BindReferences = 2,
        /// <summary>Validates that each tracked reference resolved uniquely.</summary>
        ValidateReferences = 3,
        /// <summary>Initializes components and traverses the resulting live child hierarchy.</summary>
        Initialize = 4,
        /// <summary>Resolves complete viewport, canvas and anchor layout.</summary>
        ResolveLayout = 5,
        /// <summary>Runs explicit presentation-readiness callbacks without gameplay or input updates.</summary>
        PreparePresentation = 6,
        /// <summary>Finalizes owned assets and activates complete roots in one publication step.</summary>
        Publish = 7
    }
}
