namespace helengine {
    /// <summary>Supplies monotonic elapsed milliseconds to cooperative scene loading without coupling its scheduler to a host clock.</summary>
    public interface IRuntimeSceneLoadingClock {
        /// <summary>Gets monotonically increasing milliseconds; implementations must not move backwards during an advance.</summary>
        double ElapsedMilliseconds { get; }
    }
}
