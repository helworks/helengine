namespace helengine {
    /// <summary>
    /// Stable numeric identifiers for the Core phases exposed to native CPU profilers.
    /// </summary>
    public enum RuntimeCpuProfileStage : byte {
        EarlyInput = 1,
        FrameCounters = 2,
        ObjectManagerUpdate = 3,
        Audio = 4,
        Physics = 5,
        LateInput = 6,
        PointerInteraction = 7,
        FrameBoundary = 8,
        RenderManager3D = 9,
        /// <summary>Checks command-list reuse, resets builder state and finalizes reusable cache state in separate nonoverlapping segments.</summary>
        CommandListSetup = 10,
        /// <summary>Filters one drawable and resolves its clipping chain and command transitions.</summary>
        CommandListClipping = 11,
        /// <summary>Emits one sprite's resolved geometry and textured command.</summary>
        CommandListSprites = 12,
        /// <summary>Resolves one text drawable's wrapping, metrics, position and line alignment.</summary>
        CommandListTextLayout = 13,
        /// <summary>Builds one text drawable's optional shadow and outline effect passes.</summary>
        CommandListTextEffectsSetup = 14,
        /// <summary>Emits an entire text drawable's glyph/effect command loop and releases its temporary effect passes.</summary>
        CommandListGlyphEmission = 15,
        /// <summary>Emits one rounded rectangle's resolved geometry and shape command.</summary>
        CommandListRoundedRects = 16,
        /// <summary>Closes the final drawable's remaining clip stack after ordered queue traversal.</summary>
        CommandListTrailingClips = 17
    }
}
