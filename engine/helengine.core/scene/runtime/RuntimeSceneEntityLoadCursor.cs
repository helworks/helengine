namespace helengine {
    /// <summary>Borrows scene payload and entity references while materialization resumes between components and children.</summary>
    internal sealed class RuntimeSceneEntityLoadCursor {
        /// <summary>Gets the payload borrowed until the enclosing operation finishes.</summary>
        [NativeBorrowedReturn]
        public SceneEntityAsset Asset { get; }
        /// <summary>Gets the entity owned by its root hierarchy, never by this cursor.</summary>
        [NativeBorrowedReturn]
        public Entity Entity { get; }
        /// <summary>Gets the authored root index used in diagnostics.</summary>
        public int RootIndex { get; }
        /// <summary>Gets the hierarchy depth used in diagnostics.</summary>
        public int Depth { get; }
        /// <summary>Gets or sets the next component record to deserialize.</summary>
        public int NextComponent { get; set; }
        /// <summary>Gets or sets the next child payload to create.</summary>
        public int NextChild { get; set; }
        /// <summary>Gets or sets the initial component count; components added by callbacks initialize through Entity.AddComponent.</summary>
        public int ComponentLimit { get; set; }
        /// <summary>Creates a cursor borrowing already-owned payload and hierarchy nodes.</summary>
        internal RuntimeSceneEntityLoadCursor([NativeRetainsBorrow] SceneEntityAsset asset,
            [NativeRetainsBorrow] Entity entity, int rootIndex, int depth) {
            Asset = asset;
            Entity = entity;
            RootIndex = rootIndex;
            Depth = depth;
        }
    }
}
