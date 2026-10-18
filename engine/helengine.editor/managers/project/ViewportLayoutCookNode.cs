using helengine.baseplatform.Builders;

namespace helengine.editor {
    /// <summary>Holds detached effective participants and virtual bounds while cooking one packaged scene.</summary>
    internal sealed class ViewportLayoutCookNode {
        /// <summary>Packaged scene entity whose final transform may be written.</summary>
        public SceneEntityAsset Asset;
        /// <summary>Parent cook node, without constructing a live Entity.</summary>
        public ViewportLayoutCookNode Parent;
        /// <summary>Detached built-in components; gameplay components never enter this list.</summary>
        public readonly List<Component> Components = new List<Component>();
        /// <summary>Child nodes in source hierarchy order.</summary>
        public readonly List<ViewportLayoutCookNode> Children = new List<ViewportLayoutCookNode>();
        /// <summary>Viewport declared on this entity, when present.</summary>
        public ViewportComponent Viewport;
        /// <summary>Legacy fit declared on this entity, when present.</summary>
        public ReferenceCanvasFitComponent Fit;
        /// <summary>Nearest viewport used to resolve the legacy fit policy.</summary>
        public ViewportLayoutCookNode ViewportOwner;
        /// <summary>Owner that controls position and component geometry.</summary>
        public ViewportLayoutCookNode GeometryOwner;
        /// <summary>Presentation rectangle resolved for this viewport.</summary>
        public PlatformLayoutTarget ViewportTarget;
        /// <summary>Viewport canvas metrics independent of inherited geometry ownership.</summary>
        public ViewportLayoutMetrics ViewportMetrics;
        /// <summary>Effective geometry factors inherited by participants.</summary>
        public ViewportLayoutMetrics Metrics;
        /// <summary>Indicates that geometry metrics are statically known.</summary>
        public bool MetricsKnown;
        /// <summary>Marks geometry that is baked rather than left authored for runtime.</summary>
        public bool BakeGeometry;
        /// <summary>First runtime-generated control whose detached visuals cannot yet be baked safely.</summary>
        public string UnsupportedGeneratedControlTypeId;
        /// <summary>Virtual resolved dimensions also used for parents left Dynamic or OnLoad.</summary>
        public int2 ResolvedSize;
        /// <summary>Virtual local position, committed only for cooked participants.</summary>
        public float3 ResolvedPosition;
    }
}
