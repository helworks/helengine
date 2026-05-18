namespace helengine {
    /// <summary>
    /// Describes explicit native-ownership transitions that the C++ backend must lower into deterministic delete and dispose operations.
    /// </summary>
    internal static class NativeOwnership {
        /// <summary>
        /// Releases one managed reference in source code while signaling the native backend to delete the target object.
        /// </summary>
        /// <typeparam name="T">Reference type that should be released.</typeparam>
        /// <param name="value">Reference that should be cleared.</param>
        public static void Release<T>(ref T value) where T : class {
            value = null;
        }

        /// <summary>
        /// Disposes one managed reference in source code while signaling the native backend to delete the target object and clear the reference.
        /// </summary>
        /// <typeparam name="T">Disposable reference type that should be disposed and released.</typeparam>
        /// <param name="value">Reference that should be disposed and cleared.</param>
        public static void DisposeAndRelease<T>(ref T value) where T : class, IDisposable {
            if (value != null) {
                value.Dispose();
            }

            value = null;
        }

        /// <summary>
        /// Signals the native backend to delete one temporary reference while leaving managed source semantics unchanged.
        /// </summary>
        /// <typeparam name="T">Reference type that should be deleted on the native side.</typeparam>
        /// <param name="value">Reference that should be deleted natively.</param>
        public static void Delete<T>(T value) where T : class {
        }

        /// <summary>
        /// Signals the native backend to dispose and delete one temporary reference while preserving ordinary managed disposal semantics.
        /// </summary>
        /// <typeparam name="T">Disposable reference type that should be disposed and deleted.</typeparam>
        /// <param name="value">Reference that should be disposed and deleted natively.</param>
        public static void DisposeAndDelete<T>(T value) where T : class, IDisposable {
            if (value != null) {
                value.Dispose();
            }
        }

        /// <summary>
        /// Deletes every reference stored in one managed array before clearing the array reference itself.
        /// </summary>
        /// <typeparam name="T">Reference type stored in the array.</typeparam>
        /// <param name="values">Array whose elements and container should be released.</param>
        public static void DeleteItemsAndRelease<T>(ref T[] values) where T : class {
            if (values != null) {
                for (int index = 0; index < values.Length; index++) {
                    Delete(values[index]);
                }
            }

            Release(ref values);
        }

        /// <summary>
        /// Disposes and deletes every reference stored in one managed array before clearing the array reference itself.
        /// </summary>
        /// <typeparam name="T">Disposable reference type stored in the array.</typeparam>
        /// <param name="values">Array whose elements and container should be disposed and released.</param>
        public static void DisposeItemsAndRelease<T>(ref T[] values) where T : class, IDisposable {
            if (values != null) {
                for (int index = 0; index < values.Length; index++) {
                    DisposeAndDelete(values[index]);
                }
            }

            Release(ref values);
        }
    }
}
