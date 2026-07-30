namespace helengine {
    /// <summary>
    /// Owns fixed-capacity parallel hot and cold body storage guarded by generational handles.
    /// </summary>
    sealed class HelPhysicsBodyPool3D {
        /// <summary>
        /// Stores the minimum number of addressable body slots a pool may own.
        /// </summary>
        const int MinimumCapacity = 1;

        /// <summary>
        /// Stores the largest supported body-slot count while reserving invalid handle index values.
        /// </summary>
        const int MaximumCapacity = 65534;

        /// <summary>
        /// Stores mutable hot state indexed by fixed body slot.
        /// </summary>
        readonly HelPhysicsBodyState3D[] States;

        /// <summary>
        /// Stores cold metadata parallel to <see cref="States"/>.
        /// </summary>
        readonly HelPhysicsBodyColdState3D[] ColdStates;

        /// <summary>
        /// Stores the current generation for each fixed body slot.
        /// </summary>
        readonly ushort[] Generations;

        /// <summary>
        /// Stores reusable body-slot indices as a stack.
        /// </summary>
        readonly ushort[] FreeIndices;

        /// <summary>
        /// Stores the number of currently available entries in <see cref="FreeIndices"/>.
        /// </summary>
        int FreeIndexCount;

        /// <summary>
        /// Stores the number of body slots currently allocated to live bodies.
        /// </summary>
        int ActiveCountValue;

        /// <summary>
        /// Initializes all fixed storage arrays and seeds the free list for ascending initial allocation order.
        /// </summary>
        /// <param name="capacity">Number of body slots to allocate permanently for this pool.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is outside the supported range.</exception>
        public HelPhysicsBodyPool3D(int capacity) {
            ValidateCapacity(capacity);

            States = new HelPhysicsBodyState3D[capacity];
            ColdStates = new HelPhysicsBodyColdState3D[capacity];
            Generations = new ushort[capacity];
            FreeIndices = new ushort[capacity];
            FreeIndexCount = capacity;

            for (int index = 0; index < capacity; index++) {
                FreeIndices[index] = (ushort)(capacity - index - 1);
            }
        }

        /// <summary>
        /// Gets the number of body slots currently allocated to live bodies.
        /// </summary>
        public int ActiveCount => ActiveCountValue;

        /// <summary>
        /// Gets the fixed number of body slots allocated for the lifetime of this pool.
        /// </summary>
        public int Capacity => States.Length;

        /// <summary>
        /// Allocates one free body slot and stores both explicitly supplied hot and cold state.
        /// </summary>
        /// <param name="state">Hot simulation state for the new body.</param>
        /// <param name="coldState">Cold metadata for the new body.</param>
        /// <returns>A generational handle that accesses the allocated body slot.</returns>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when every fixed body slot is occupied.</exception>
        public HelPhysicsBodyHandle3D Allocate(HelPhysicsBodyState3D state, HelPhysicsBodyColdState3D coldState) {
            if (FreeIndexCount == 0) {
                throw new HelPhysicsCapacityExceededException("body", States.Length);
            }

            ushort index = FreeIndices[--FreeIndexCount];
            state.IsOccupied = true;
            States[index] = state;
            ColdStates[index] = coldState;
            ActiveCountValue++;

            return new HelPhysicsBodyHandle3D(index, Generations[index]);
        }

        /// <summary>
        /// Releases a live body slot, invalidates its generation, and returns its index to the free list.
        /// </summary>
        /// <param name="handle">Current generational handle for the body slot to release.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> is invalid, stale, already released, or cannot advance beyond the final representable generation.</exception>
        public void Release(HelPhysicsBodyHandle3D handle) {
            ValidateHandle(handle);

            if (Generations[handle.Index] == ushort.MaxValue) {
                throw new InvalidOperationException("The body handle generation is exhausted and cannot be reissued.");
            }

            States[handle.Index].IsOccupied = false;
            Generations[handle.Index]++;
            FreeIndices[FreeIndexCount++] = handle.Index;
            ActiveCountValue--;
        }

        /// <summary>
        /// Returns mutable hot state for one currently allocated body slot.
        /// </summary>
        /// <param name="handle">Current generational handle for the requested body.</param>
        /// <returns>A reference to the requested body's hot simulation state.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> is invalid, stale, or released.</exception>
        public ref HelPhysicsBodyState3D GetRequiredState(HelPhysicsBodyHandle3D handle) {
            ValidateHandle(handle);

            return ref States[handle.Index];
        }

        /// <summary>
        /// Returns mutable cold metadata for one currently allocated body slot.
        /// </summary>
        /// <param name="handle">Current generational handle for the requested body.</param>
        /// <returns>A reference to the requested body's cold metadata.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> is invalid, stale, or released.</exception>
        public ref HelPhysicsBodyColdState3D GetRequiredColdState(HelPhysicsBodyHandle3D handle) {
            ValidateHandle(handle);

            return ref ColdStates[handle.Index];
        }

        /// <summary>
        /// Determines whether one validated fixed body index currently owns a live body.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot index to inspect.</param>
        /// <returns><see langword="true"/> when the slot is occupied; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        public bool IsOccupied(int bodyIndex) {
            ValidateBodyIndex(bodyIndex);

            return States[bodyIndex].IsOccupied;
        }

        /// <summary>
        /// Returns the complete generational identity of the live body occupying one fixed slot.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot whose current occupant identity is required.</param>
        /// <returns>A handle containing the occupied slot index and its current generation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="bodyIndex"/> identifies an unoccupied slot.</exception>
        public HelPhysicsBodyHandle3D GetRequiredHandleByIndex(int bodyIndex) {
            ValidateOccupiedBodyIndex(bodyIndex);

            return new HelPhysicsBodyHandle3D((ushort)bodyIndex, Generations[bodyIndex]);
        }

        /// <summary>
        /// Returns mutable hot state for one occupied fixed body index without constructing a handle.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot index whose live state is required.</param>
        /// <returns>A reference to the live body's hot simulation state.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="bodyIndex"/> identifies an unoccupied slot.</exception>
        public ref HelPhysicsBodyState3D GetRequiredStateByIndex(int bodyIndex) {
            ValidateOccupiedBodyIndex(bodyIndex);

            return ref States[bodyIndex];
        }

        /// <summary>
        /// Returns mutable cold metadata for one occupied fixed body index without constructing a handle.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot index whose live metadata is required.</param>
        /// <returns>A reference to the live body's cold metadata.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="bodyIndex"/> identifies an unoccupied slot.</exception>
        public ref HelPhysicsBodyColdState3D GetRequiredColdStateByIndex(int bodyIndex) {
            ValidateOccupiedBodyIndex(bodyIndex);

            return ref ColdStates[bodyIndex];
        }

        /// <summary>
        /// Validates that a requested capacity fits the fixed handle-addressable body-pool range.
        /// </summary>
        /// <param name="capacity">Requested number of permanent body slots.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is outside the supported range.</exception>
        static void ValidateCapacity(int capacity) {
            if (capacity < MinimumCapacity || capacity > MaximumCapacity) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Body pool capacities must be between 1 and 65,534 inclusive.");
            }
        }

        /// <summary>
        /// Validates that an integer body index addresses one slot in this pool's fixed arrays.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot index to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        void ValidateBodyIndex(int bodyIndex) {
            if (bodyIndex < 0 || bodyIndex >= States.Length) {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "The body index does not identify a slot in this pool.");
            }
        }

        /// <summary>
        /// Validates that an integer body index addresses the live occupant of one fixed pool slot.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot index whose occupancy must be current.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> lies outside this pool.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="bodyIndex"/> identifies an unoccupied slot.</exception>
        void ValidateOccupiedBodyIndex(int bodyIndex) {
            ValidateBodyIndex(bodyIndex);

            if (!States[bodyIndex].IsOccupied) {
                throw new InvalidOperationException("The body index refers to an unoccupied slot.");
            }
        }

        /// <summary>
        /// Validates that a handle identifies the live occupant of one slot in this pool.
        /// </summary>
        /// <param name="handle">Handle whose index, generation, and occupancy must all be current.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="handle"/> does not identify a live body slot.</exception>
        void ValidateHandle(HelPhysicsBodyHandle3D handle) {
            if (handle.Index == ushort.MaxValue || handle.Index >= States.Length) {
                throw new InvalidOperationException("The body handle index does not identify a slot in this pool.");
            }

            if (Generations[handle.Index] != handle.Generation) {
                throw new InvalidOperationException("The body handle generation is stale.");
            }

            if (!States[handle.Index].IsOccupied) {
                throw new InvalidOperationException("The body handle refers to a released slot.");
            }
        }
    }
}
