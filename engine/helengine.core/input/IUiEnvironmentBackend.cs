namespace helengine {
    /// <summary>Host-owned allocation-free UI facts in logical points. The native host refreshes these before Core.Update; C# chooses layout.</summary>
    public interface IUiEnvironmentBackend {
        /// <summary>Gets a change revision for geometry, orientation, appearance, preferred text size, motion and keyboard updates.</summary>
        int Revision { get; }
        /// <summary>Changes once when the host returns from the background, independently of geometry and keyboard changes.</summary>
        int ForegroundRevision { get; }
        /// <summary>Gets the current view width and height in logical points, independently of any reference canvas.</summary>
        float2 LogicalSize { get; }
        /// <summary>Gets drawable pixels per logical point, independently of reference-canvas scaling.</summary>
        float DrawableScale { get; }
        /// <summary>Gets left, top, right and bottom safe-area insets in logical points.</summary>
        float4 SafeAreaInsets { get; }
        /// <summary>Gets whether UIKit currently resolves the view's appearance as dark.</summary>
        bool DarkAppearance { get; }
        /// <summary>Gets the system preference for reduced motion.</summary>
        bool ReducedMotion { get; }
        /// <summary>Gets keyboard overlap x, y, width and height in view-local logical points; zero size means no overlap.</summary>
        float4 KeyboardOcclusion { get; }
        /// <summary>Gets the cached preferred content-size category; its changes always increment Revision.</summary>
        string ContentSizeCategory { get; }
    }
}
