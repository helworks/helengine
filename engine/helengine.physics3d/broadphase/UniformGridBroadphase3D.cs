namespace helengine {
    /// <summary>
    /// Uses a fixed spatial grid to reduce dynamic-body collision work to nearby candidate pairs.
    /// </summary>
    public sealed class UniformGridBroadphase3D : IBroadphase3D {
        /// <summary>
        /// Initializes one uniform-grid broadphase.
        /// </summary>
        /// <param name="cellSize">World-space cell size used for occupancy bucketing.</param>
        public UniformGridBroadphase3D(double cellSize) {
            if (double.IsNaN(cellSize) || double.IsInfinity(cellSize) || cellSize <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Broadphase cell size must be a finite value greater than zero.");
            }

            CellSize = cellSize;
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

            Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell = BuildOccupantIndicesByCell(bodyStates);
            HashSet<long> uniquePairKeys = new HashSet<long>();
            List<BodyPair3D> candidatePairs = new List<BodyPair3D>();

            for (int bodyIndex = 0; bodyIndex < bodyStates.Count; bodyIndex++) {
                BodyState3D bodyState = bodyStates[bodyIndex];
                if (bodyState.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                AppendDynamicBodyPairs(bodyStates, occupantIndicesByCell, uniquePairKeys, candidatePairs, bodyIndex);
            }

            return candidatePairs;
        }

        /// <summary>
        /// Builds the cell-to-occupants mapping for the supplied body states.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        /// <returns>Dictionary of occupied cells to body indices.</returns>
        Dictionary<GridCellKey3D, List<int>> BuildOccupantIndicesByCell(IReadOnlyList<BodyState3D> bodyStates) {
            Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell = new Dictionary<GridCellKey3D, List<int>>();
            for (int bodyIndex = 0; bodyIndex < bodyStates.Count; bodyIndex++) {
                AddBodyOccupancy(bodyStates[bodyIndex], bodyIndex, occupantIndicesByCell);
            }

            return occupantIndicesByCell;
        }

        /// <summary>
        /// Adds one body's occupied cells to the uniform-grid map.
        /// </summary>
        /// <param name="bodyState">Body state whose bounds should be added.</param>
        /// <param name="bodyIndex">Dense body-state index.</param>
        /// <param name="occupantIndicesByCell">Destination occupancy map.</param>
        void AddBodyOccupancy(BodyState3D bodyState, int bodyIndex, Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell) {
            if (bodyState == null) {
                throw new ArgumentNullException(nameof(bodyState));
            }
            if (occupantIndicesByCell == null) {
                throw new ArgumentNullException(nameof(occupantIndicesByCell));
            }

            int minCellX = GetCellIndex(bodyState.Position.X - bodyState.HalfExtents.X);
            int maxCellX = GetCellIndex(bodyState.Position.X + bodyState.HalfExtents.X);
            int minCellY = GetCellIndex(bodyState.Position.Y - bodyState.HalfExtents.Y);
            int maxCellY = GetCellIndex(bodyState.Position.Y + bodyState.HalfExtents.Y);
            int minCellZ = GetCellIndex(bodyState.Position.Z - bodyState.HalfExtents.Z);
            int maxCellZ = GetCellIndex(bodyState.Position.Z + bodyState.HalfExtents.Z);

            for (int x = minCellX; x <= maxCellX; x++) {
                for (int y = minCellY; y <= maxCellY; y++) {
                    for (int z = minCellZ; z <= maxCellZ; z++) {
                        AddOccupantIndexToCell(occupantIndicesByCell, new GridCellKey3D(x, y, z), bodyIndex);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the supplied body index to one occupied cell.
        /// </summary>
        /// <param name="occupantIndicesByCell">Destination occupancy map.</param>
        /// <param name="cellKey">Cell receiving the body index.</param>
        /// <param name="bodyIndex">Dense body-state index.</param>
        void AddOccupantIndexToCell(Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell, GridCellKey3D cellKey, int bodyIndex) {
            if (!occupantIndicesByCell.TryGetValue(cellKey, out List<int> occupantIndices)) {
                occupantIndices = new List<int>();
                occupantIndicesByCell.Add(cellKey, occupantIndices);
            }

            occupantIndices.Add(bodyIndex);
        }

        /// <summary>
        /// Adds all nearby candidate pairs for one dynamic body.
        /// </summary>
        /// <param name="bodyStates">Dense body-state list bound to the world.</param>
        /// <param name="occupantIndicesByCell">Current occupancy map.</param>
        /// <param name="uniquePairKeys">Set used to remove duplicate cell hits.</param>
        /// <param name="candidatePairs">Destination candidate-pair list.</param>
        /// <param name="bodyIndex">Dynamic body index whose neighbors should be appended.</param>
        void AppendDynamicBodyPairs(
            IReadOnlyList<BodyState3D> bodyStates,
            Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell,
            HashSet<long> uniquePairKeys,
            List<BodyPair3D> candidatePairs,
            int bodyIndex) {
            BodyState3D bodyState = bodyStates[bodyIndex];
            int minCellX = GetCellIndex(bodyState.Position.X - bodyState.HalfExtents.X);
            int maxCellX = GetCellIndex(bodyState.Position.X + bodyState.HalfExtents.X);
            int minCellY = GetCellIndex(bodyState.Position.Y - bodyState.HalfExtents.Y);
            int maxCellY = GetCellIndex(bodyState.Position.Y + bodyState.HalfExtents.Y);
            int minCellZ = GetCellIndex(bodyState.Position.Z - bodyState.HalfExtents.Z);
            int maxCellZ = GetCellIndex(bodyState.Position.Z + bodyState.HalfExtents.Z);

            for (int x = minCellX; x <= maxCellX; x++) {
                for (int y = minCellY; y <= maxCellY; y++) {
                    for (int z = minCellZ; z <= maxCellZ; z++) {
                        AppendCellPairs(occupantIndicesByCell, uniquePairKeys, candidatePairs, bodyIndex, new GridCellKey3D(x, y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Appends unique candidate pairs for one body against the occupants of a specific cell.
        /// </summary>
        /// <param name="occupantIndicesByCell">Current occupancy map.</param>
        /// <param name="uniquePairKeys">Set used to remove duplicate cell hits.</param>
        /// <param name="candidatePairs">Destination candidate-pair list.</param>
        /// <param name="bodyIndex">Dense body-state index whose neighbors are being queried.</param>
        /// <param name="cellKey">Occupied cell to inspect.</param>
        void AppendCellPairs(
            Dictionary<GridCellKey3D, List<int>> occupantIndicesByCell,
            HashSet<long> uniquePairKeys,
            List<BodyPair3D> candidatePairs,
            int bodyIndex,
            GridCellKey3D cellKey) {
            if (!occupantIndicesByCell.TryGetValue(cellKey, out List<int> occupantIndices)) {
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
                if (!uniquePairKeys.Add(pairKey)) {
                    continue;
                }

                candidatePairs.Add(new BodyPair3D(firstBodyIndex, secondBodyIndex));
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
            return (int)Math.Floor(coordinate / CellSize);
        }
    }
}
