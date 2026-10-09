namespace helengine.timeline.runtime {
    /// <summary>
    /// Releases arrays owned by cooked timeline objects in the way native builds expect: every element is deleted, then
    /// the array itself, while the shared empty-array singleton is never deleted.
    /// </summary>
    static class CookedTimelineOwnership {
        /// <summary>
        /// Deletes the elements and the container of an owned array of plain data objects.
        /// </summary>
        /// <typeparam name="T">Element type of the array.</typeparam>
        /// <param name="values">Array owned by the caller; may be null or the shared empty array.</param>
        internal static void DeleteArray<T>(T[] values) where T : class {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            for (int index = 0; index < values.Length; index++) {
                NativeOwnership.Delete(values[index]);
            }
            NativeOwnership.Delete(values);
        }

        /// <summary>
        /// Disposes the elements and deletes the container of an owned array of disposable objects.
        /// </summary>
        /// <typeparam name="T">Element type of the array.</typeparam>
        /// <param name="values">Array owned by the caller; may be null or the shared empty array.</param>
        internal static void DisposeArray<T>(T[] values) where T : class, IDisposable {
            if (values == null || ReferenceEquals(values, Array.Empty<T>())) {
                return;
            }
            for (int index = 0; index < values.Length; index++) {
                NativeOwnership.DisposeAndDelete(values[index]);
            }
            NativeOwnership.Delete(values);
        }
    }
}
