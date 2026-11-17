using System;

namespace helengine.ui.format {
    /// <summary>One named finite logical layout input.</summary>
    public sealed class UiMetric {
        /// <summary>Unique nonempty metric name.</summary>
        public string Name = "";
        /// <summary>Finite logical value; negative values are permitted.</summary>
        public float Value = 0;
    }
}
