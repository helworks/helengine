namespace helengine {
    /// <summary>
    /// Describes one platform-specific diagnostics metric attached to a shared runtime snapshot.
    /// </summary>
    public sealed class RuntimeDiagnosticsMetric {
        /// <summary>
        /// Initializes one diagnostics metric with a stable name and numeric value.
        /// </summary>
        /// <param name="name">Stable metric name.</param>
        /// <param name="value">Numeric metric value.</param>
        public RuntimeDiagnosticsMetric(string name, ulong value) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Metric name must be provided.", nameof(name));
            }

            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets the stable metric name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the numeric metric value.
        /// </summary>
        public ulong Value { get; }
    }
}
