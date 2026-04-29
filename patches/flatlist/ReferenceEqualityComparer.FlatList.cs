namespace helengine {
    /// <summary>
    /// Reference equality comparer to avoid overriding Equals on drawables.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
        /// <summary>
        /// Shared singleton instance.
        /// </summary>
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        /// <summary>
        /// Prevents external instantiation to enforce singleton usage.
        /// </summary>
        ReferenceEqualityComparer() { }

        /// <summary>
        /// Compares two references for identity equality.
        /// </summary>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <returns><c>true</c> if both references point to the same instance; otherwise, <c>false</c>.</returns>
        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        /// <summary>
        /// Gets a hash code based on the object's reference.
        /// </summary>
        /// <param name="obj">Object to hash.</param>
        /// <returns>Reference-based hash code.</returns>
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
