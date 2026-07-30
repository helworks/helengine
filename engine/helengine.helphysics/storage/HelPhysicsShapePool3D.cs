namespace helengine {
    /// <summary>
    /// Owns fixed-capacity box-shape storage guarded by generational handles.
    /// </summary>
    sealed class HelPhysicsShapePool3D {
        /// <summary>
        /// Stores the minimum number of addressable shape slots a pool may own.
        /// </summary>
        const int MinimumCapacity = 1;

        /// <summary>
        /// Stores the largest supported shape-slot count while reserving the invalid handle index value.
        /// </summary>
        const int MaximumCapacity = 65534;

        /// <summary>
        /// Stores box shape values indexed by their permanent fixed slot.
        /// </summary>
        readonly HelPhysicsBoxShape3D[] Boxes;

        /// <summary>
        /// Stores whether each corresponding entry in <see cref="Boxes"/> currently belongs to a live handle.
        /// </summary>
        readonly bool[] IsOccupied;

        /// <summary>
        /// Stores the current generation for each fixed shape slot.
        /// </summary>
        readonly ushort[] Generations;

        /// <summary>
        /// Stores reusable shape-slot indices as a stack.
        /// </summary>
        readonly ushort[] FreeIndices;

        /// <summary>
        /// Stores the number of currently available entries in <see cref="FreeIndices"/>.
        /// </summary>
        int FreeIndexCount;

        /// <summary>
        /// Stores the number of shape slots currently allocated to live boxes.
        /// </summary>
        int ActiveCountValue;

        /// <summary>
        /// Initializes all fixed storage arrays and seeds the free list for ascending initial allocation order.
        /// </summary>
        /// <param name="capacity">Number of shape slots to allocate permanently for this pool.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is outside the supported range.</exception>
        public HelPhysicsShapePool3D(int capacity) {
            ValidateCapacity(capacity);

            Boxes = new HelPhysicsBoxShape3D[capacity];
            IsOccupied = new bool[capacity];
            Generations = new ushort[capacity];
            FreeIndices = new ushort[capacity];
            FreeIndexCount = capacity;

            for (int index = 0; index < capacity; index++) {
                FreeIndices[index] = (ushort)(capacity - index - 1);
            }
        }

        /// <summary>
        /// Gets the number of shape slots currently allocated to live boxes.
        /// </summary>
        public int ActiveCount => ActiveCountValue;

        /// <summary>
        /// Gets the fixed number of box-shape slots allocated for the lifetime of this pool.
        /// </summary>
        public int Capacity => Boxes.Length;

        /// <summary>
        /// Allocates one free shape slot and stores the supplied box value.
        /// </summary>
        /// <param name="box">Box shape to store in the newly allocated slot.</param>
        /// <returns>A generational handle that accesses the allocated shape slot.</returns>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when every fixed shape slot is occupied.</exception>
        public HelPhysicsShapeHandle3D Allocate(HelPhysicsBoxShape3D box) {
            if (FreeIndexCount == 0) {
                throw new HelPhysicsCapacityExceededException("shape", Boxes.Length);
            }

            ushort index = FreeIndices[--FreeIndexCount];
            Boxes[index] = box;
            IsOccupied[index] = true;
            ActiveCountValue++;

            return new HelPhysicsShapeHandle3D(index, Generations[index]);
        }

        /// <summary>
        /// Releases a live shape slot, invalidates its generation, and returns its index to the free list.
        /// </summary>
        /// <param name="handle">Current generational handle for the shape slot to release.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> is invalid, stale, released, or cannot advance beyond the final representable generation.</exception>
        public void Release(HelPhysicsShapeHandle3D handle) {
            ValidateHandle(handle);

            if (Generations[handle.Index] == ushort.MaxValue) {
                throw new InvalidOperationException("The shape handle generation is exhausted and cannot be reissued.");
            }

            IsOccupied[handle.Index] = false;
            Generations[handle.Index]++;
            FreeIndices[FreeIndexCount++] = handle.Index;
            ActiveCountValue--;
        }

        /// <summary>
        /// Returns the stored box value for one currently allocated shape slot.
        /// </summary>
        /// <param name="handle">Current generational handle for the requested box.</param>
        /// <returns>A reference to the requested box value in fixed storage.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> is invalid, stale, or released.</exception>
        public ref HelPhysicsBoxShape3D GetRequiredBox(HelPhysicsShapeHandle3D handle) {
            ValidateHandle(handle);

            return ref Boxes[handle.Index];
        }

        /// <summary>
        /// Validates that a requested capacity fits the fixed handle-addressable shape-pool range.
        /// </summary>
        /// <param name="capacity">Requested number of permanent shape slots.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is outside the supported range.</exception>
        static void ValidateCapacity(int capacity) {
            if (capacity < MinimumCapacity || capacity > MaximumCapacity) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Shape pool capacities must be between 1 and 65,534 inclusive.");
            }
        }

        /// <summary>
        /// Validates that a handle identifies the live box occupying one slot in this pool.
        /// </summary>
        /// <param name="handle">Handle whose index, generation, and occupancy must all be current.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> does not identify a live shape slot.</exception>
        void ValidateHandle(HelPhysicsShapeHandle3D handle) {
            if (handle.Index == ushort.MaxValue || handle.Index >= Boxes.Length) {
                throw new InvalidOperationException("The shape handle index does not identify a slot in this pool.");
            }

            if (Generations[handle.Index] != handle.Generation) {
                throw new InvalidOperationException("The shape handle generation is stale.");
            }

            if (!IsOccupied[handle.Index]) {
                throw new InvalidOperationException("The shape handle refers to a released slot.");
            }
        }
    }
}
