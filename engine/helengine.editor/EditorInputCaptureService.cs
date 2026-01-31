namespace helengine.editor {
    /// <summary>
    /// Tracks UI regions that should block viewport input when the cursor is inside them.
    /// </summary>
    public static class EditorInputCaptureService {
        /// <summary>
        /// Stores registered input blockers keyed by their owner.
        /// </summary>
        static readonly Dictionary<object, InputBlocker> Blockers = new Dictionary<object, InputBlocker>();

        /// <summary>
        /// Registers or updates a blocking rectangle for the specified owner.
        /// </summary>
        /// <param name="owner">Owning object that manages the blocking region.</param>
        /// <param name="position">Top-left window position of the blocking region.</param>
        /// <param name="size">Size of the blocking region in pixels.</param>
        public static void SetBlocker(object owner, int2 position, int2 size) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }
            if (size.X <= 0 || size.Y <= 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Blocker size must be positive.");
            }

            if (!Blockers.TryGetValue(owner, out InputBlocker blocker)) {
                blocker = new InputBlocker(owner);
                Blockers.Add(owner, blocker);
            }

            blocker.Position = position;
            blocker.Size = size;
        }

        /// <summary>
        /// Removes a previously registered blocking region.
        /// </summary>
        /// <param name="owner">Owning object that registered the region.</param>
        public static void ClearBlocker(object owner) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            Blockers.Remove(owner);
        }

        /// <summary>
        /// Determines whether the provided pointer position is inside any registered blocker.
        /// </summary>
        /// <param name="position">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is within a blocking region.</returns>
        public static bool IsPointerBlocked(int2 position) {
            foreach (var entry in Blockers.Values) {
                if (entry.Contains(position)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the pointer is inside any registered blocker that matches the provided predicate.
        /// </summary>
        /// <param name="position">Pointer position in window coordinates.</param>
        /// <param name="ownerPredicate">Predicate that returns true when a blocker owner should be considered.</param>
        /// <returns>True when the pointer is within a matching blocking region.</returns>
        public static bool IsPointerBlocked(int2 position, Func<object, bool> ownerPredicate) {
            if (ownerPredicate == null) {
                throw new ArgumentNullException(nameof(ownerPredicate));
            }

            foreach (var entry in Blockers.Values) {
                if (!ownerPredicate(entry.Owner)) {
                    continue;
                }

                if (entry.Contains(position)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Represents a window-space rectangle used to block viewport input.
        /// </summary>
        class InputBlocker {
            /// <summary>
            /// Initializes a new blocker for the specified owner.
            /// </summary>
            /// <param name="owner">Owning object for this blocker.</param>
            public InputBlocker(object owner) {
                if (owner == null) {
                    throw new ArgumentNullException(nameof(owner));
                }

                Owner = owner;
            }

            /// <summary>
            /// Gets the owner that registered this blocker.
            /// </summary>
            public object Owner { get; }
            /// <summary>
            /// Gets or sets the top-left window position of the blocker.
            /// </summary>
            public int2 Position { get; set; }
            /// <summary>
            /// Gets or sets the size of the blocker in pixels.
            /// </summary>
            public int2 Size { get; set; }

            /// <summary>
            /// Determines whether the provided point lies within the blocker bounds.
            /// </summary>
            /// <param name="point">Pointer position to test.</param>
            /// <returns>True when the point is inside the bounds.</returns>
            public bool Contains(int2 point) {
                int left = Position.X;
                int top = Position.Y;
                int right = Position.X + Size.X;
                int bottom = Position.Y + Size.Y;

                return point.X >= left && point.X < right &&
                       point.Y >= top && point.Y < bottom;
            }
        }
    }
}
