namespace helengine {
    /// <summary>
    /// Uses a fixed spatial grid to reduce dynamic-body collision work to nearby candidate pairs.
    /// </summary>
    public sealed class UniformGridBroadphase3D : IBroadphase3D {
        /// <summary>
        /// Reusable cell occupancy map populated during each broadphase query.
        /// </summary>
        readonly Dictionary<GridCellKey3D, List<int>> OccupantIndicesByCell;

        /// <summary>
        /// Reusable unique-pair key list used to deduplicate candidate pairs.
        /// </summary>
        readonly List<long> UniquePairKeys;

        /// <summary>
        /// Reusable candidate-pair list returned from the most recent query.
        /// </summary>
        readonly List<BodyPair3D> CandidatePairs;

        /// <summary>
        /// Reusable list of cells populated during the previous query so only active buckets are cleared.
        /// </summary>
        readonly List<GridCellKey3D> ActiveCellKeys;

        /// <summary>
        /// Initializes one uniform-grid broadphase.
        /// </summary>
        /// <param name="cellSize">World-space cell size used for occupancy bucketing.</param>
        public UniformGridBroadphase3D(double cellSize) {
            if (double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Broadphase cell size must be a finite value greater than zero.");
            }

            CellSize = cellSize;
            OccupantIndicesByCell = new Dictionary<GridCellKey3D, List<int>>();
            UniquePairKeys = new List<long>();
            CandidatePairs = new List<BodyPair3D>();
            ActiveCellKeys = new List<GridCellKey3D>();
        }

        /// <summary>
        /// Gets the world-space cell size used for occupancy bucketing.
        /// </summary>
        public double CellSize { get; }

        /// <summary>
        /// Collects candidate pairs by bucketing body bounds into overlapping uniform-grid cells.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        /// <returns>Candidate body pairs that may interact this step.</returns>
        public IReadOnlyList<BodyPair3D> CollectCandidatePairs(IReadOnlyList<BodyState3D> bodyStates) {
            if (bodyStates == null) {
                throw new ArgumentNullException(nameof(bodyStates));
            }

            ClearOccupantIndicesByCell();
            UniquePairKeys.Clear();
            CandidatePairs.Clear();
            BuildOccupantIndicesByCell(bodyStates);

            for (int bodyIndex = 0; bodyIndex < bodyStates.Count; bodyIndex++) {
                BodyState3D bodyState = bodyStates[bodyIndex];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                AppendDynamicBodyPairs(bodyStates, bodyIndex);
            }

            return CandidatePairs;
        }

        /// <summary>
        /// Clears every reusable cell occupancy list before the next broadphase query repopulates it.
        /// </summary>
        void ClearOccupantIndicesByCell() {
            for (int index = 0; index < ActiveCellKeys.Count; index++) {
                GridCellKey3D cellKey = ActiveCellKeys[index];
                if (OccupantIndicesByCell.TryGetValue(cellKey, out List<int> occupantIndices)) {
                    occupantIndices.Clear();
                }
            }

            ActiveCellKeys.Clear();
        }

        /// <summary>
        /// Builds the reusable cell-to-occupants mapping for the supplied body states.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        void BuildOccupantIndicesByCell(IReadOnlyList<BodyState3D> bodyStates) {
            for (int bodyIndex = 0; bodyIndex < bodyStates.Count; bodyIndex++) {
                AddBodyOccupancy(bodyStates[bodyIndex], bodyIndex);
            }
        }

        /// <summary>
        /// Adds one body's occupied cells to the uniform-grid map.
        /// </summary>
        /// <param name="bodyState">Body state whose bounds should be added.</param>
        /// <param name="bodyIndex">Dense body-state index.</param>
        void AddBodyOccupancy(BodyState3D bodyState, int bodyIndex) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }

            int minCellX = GetCellIndex(bodyState.Position.X - bodyState.AxisAlignedHalfExtents.X);
            int maxCellX = GetCellIndex(bodyState.Position.X + bodyState.AxisAlignedHalfExtents.X);
            int minCellY = GetCellIndex(bodyState.Position.Y - bodyState.AxisAlignedHalfExtents.Y);
            int maxCellY = GetCellIndex(bodyState.Position.Y + bodyState.AxisAlignedHalfExtents.Y);
            int minCellZ = GetCellIndex(bodyState.Position.Z - bodyState.AxisAlignedHalfExtents.Z);
            int maxCellZ = GetCellIndex(bodyState.Position.Z + bodyState.AxisAlignedHalfExtents.Z);

            for (int x = minCellX; x <= maxCellX; x++) {
                for (int y = minCellY; y <= maxCellY; y++) {
                    for (int z = minCellZ; z <= maxCellZ; z++) {
                        AddOccupantIndexToCell(new GridCellKey3D(x, y, z), bodyIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the supplied body index to one occupied cell.
        /// </summary>
        /// <param name="cellKey">Cell receiving the body index.</param>
        /// <param name="bodyIndex">Dense body-state index.</param>
        void AddOccupantIndexToCell(GridCellKey3D cellKey, int bodyIndex) {
            if (!OccupantIndicesByCell.TryGetValue(cellKey, out List<int> occupantIndices)) {
                occupantIndices = new List<int>();
                OccupantIndicesByCell.Add(cellKey, occupantIndices);
            }
            if (occupantIndices.Count == 0) {
                ActiveCellKeys.Add(cellKey);
            }

            occupantIndices.Add(bodyIndex);
        }

        /// <summary>
        /// Adds all nearby candidate pairs for one dynamic body.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        /// <param name="bodyIndex">Dynamic body index whose neighbors should be appended.</param>
        void AppendDynamicBodyPairs(IReadOnlyList<BodyState3D> bodyStates, int bodyIndex) {
            BodyState3D bodyState = bodyStates[bodyIndex];
            int minCellX = GetCellIndex(bodyState.Position.X - bodyState.AxisAlignedHalfExtents.X);
            int maxCellX = GetCellIndex(bodyState.Position.X + bodyState.AxisAlignedHalfExtents.X);
            int minCellY = GetCellIndex(bodyState.Position.Y - bodyState.AxisAlignedHalfExtents.Y);
            int maxCellY = GetCellIndex(bodyState.Position.Y + bodyState.AxisAlignedHalfExtents.Y);
            int minCellZ = GetCellIndex(bodyState.Position.Z - bodyState.AxisAlignedHalfExtents.Z);
            int maxCellZ = GetCellIndex(bodyState.Position.Z + bodyState.AxisAlignedHalfExtents.Z);

            for (int x = minCellX; x <= maxCellX; x++) {
                for (int y = minCellY; y <= maxCellY; y++) {
                    for (int z = minCellZ; z <= maxCellZ; z++) {
                        AppendCellPairs(bodyIndex, new GridCellKey3D(x, y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Appends unique candidate pairs for one body against the occupants of a specific cell.
        /// </summary>
        /// <param name="bodyIndex">Dense body-state index whose neighbors are being queried.</param>
        /// <param name="cellKey">Occupied cell to inspect.</param>
        void AppendCellPairs(int bodyIndex, GridCellKey3D cellKey) {
            if (!OccupantIndicesByCell.TryGetValue(cellKey, out List<int> occupantIndices)) {
                return;
            }

            for (int occupantIndex = 0; occupantIndex < occupantIndices.Count; occupantIndex++) {
                int otherBodyIndex = occupantIndices[occupantIndex];
                if (otherBodyIndex == bodyIndex) {
                    continue;
                }

                int firstBodyIndex = Math.Min(bodyIndex, otherBodyIndex);
                int secondBodyIndex = Math.Max(bodyIndex, otherBodyIndex);
                long pairKey = CreatePairKey(firstBodyIndex, secondBodyIndex);
                if (UniquePairKeys.Contains(pairKey)) {
                    continue;
                }

                UniquePairKeys.Add(pairKey);
                CandidatePairs.Add(new BodyPair3D(firstBodyIndex, secondBodyIndex));
            }
        }

        /// <summary>
        /// Creates one unique integer key for the supplied candidate pair indices.
        /// </summary>
        /// <param name="firstBodyIndex">Lower dense body-state index.</param>
        /// <param name="secondBodyIndex">Higher dense body-state index.</param>
        /// <returns>Unique integer pair key.</returns>
        static long CreatePairKey(int firstBodyIndex, int secondBodyIndex) {
            return ((long)firstBodyIndex << 32) | (uint)secondBodyIndex;
        }

        /// <summary>
        /// Converts one world-space coordinate into a uniform-grid cell index.
        /// </summary>
        /// <param name="coordinate">World-space coordinate on one axis.</param>
        /// <returns>Containing cell index.</returns>
        int GetCellIndex(float coordinate) {
            double scaledCoordinate = coordinate / CellSize;
            int truncatedCoordinate = (int)scaledCoordinate;
            if (scaledCoordinate < truncatedCoordinate) {
                return truncatedCoordinate - 1;
            }

            return truncatedCoordinate;
        }
    }
}
