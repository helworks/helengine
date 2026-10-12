namespace helengine.editor {
    /// <summary>Owns mutable reduction topology while leaving the input buffers untouched until all limits pass.</summary>
    sealed class ModelGpuReductionWorkspace {
        /// <summary>Independent render asset committed only after successful reduction.</summary>
        readonly ModelAsset Asset;
        /// <summary>Effective render-only limits.</summary>
        readonly ModelGpuReductionSettings Settings;
        /// <summary>Geometric position groups with paired attribute wedges.</summary>
        readonly List<ModelReductionGroup> Groups = new List<ModelReductionGroup>();
        /// <summary>Original and surviving triangles in authored order.</summary>
        readonly List<ModelReductionTriangle> Triangles = new List<ModelReductionTriangle>();
        /// <summary>Geometric group corresponding to each original attribute vertex.</summary>
        readonly int[] VertexGroups;
        /// <summary>Versioned collapse queue; only changed local neighborhoods are recalculated.</summary>
        readonly PriorityQueue<ModelReductionCandidate, ModelReductionPriority> Candidates = new PriorityQueue<ModelReductionCandidate, ModelReductionPriority>();
        /// <summary>Number of currently referenced attribute vertices.</summary>
        int VertexCount;
        /// <summary>Number of surviving triangles.</summary>
        int TriangleCount;
        /// <summary>Number of surviving triangles for every authored material slot.</summary>
        readonly int[] SubmeshTriangles;
        /// <summary>Actual source extrema protected by the endpoint policy.</summary>
        readonly double[] Extrema = new double[6];
        /// <summary>Number of active geometric groups preserving every source extremum.</summary>
        readonly int[] ExtremumCounts = new int[6];

        /// <summary>Validates source geometry and creates an independent indexed topology.</summary>
        public ModelGpuReductionWorkspace(ModelAsset asset, ModelGpuReductionSettings settings) {
            Asset = asset;
            Settings = settings;
            if (asset.Positions == null || asset.Positions.Length < 3) throw new InvalidOperationException("GPU reduction requires at least three positions.");
            if (asset.Normals != null && asset.Normals.Length != 0 && asset.Normals.Length != asset.Positions.Length) throw new InvalidOperationException("GPU reduction normal count must match positions.");
            if (asset.TexCoords != null && asset.TexCoords.Length != 0 && asset.TexCoords.Length != asset.Positions.Length) throw new InvalidOperationException("GPU reduction UV count must match positions.");
            ModelAssetIndexData indices = ModelAssetIndexData.Resolve(asset);
            if (indices.IndexCount == 0 || indices.IndexCount % 3 != 0) throw new InvalidOperationException("GPU reduction requires a nonempty triangle index buffer.");
            VertexGroups = new int[asset.Positions.Length];
            Dictionary<float3, int> positions = new Dictionary<float3, int>();
            Dictionary<string, int> identicalWedges = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] canonicalVertices = new int[asset.Positions.Length];
            for (int index = 0; index < asset.Positions.Length; index++) {
                float3 position = asset.Positions[index];
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) throw new InvalidOperationException("GPU reduction positions must be finite.");
                if (!positions.TryGetValue(position, out int group)) {
                    group = Groups.Count;
                    positions.Add(position, group);
                    Groups.Add(new ModelReductionGroup { Position = position });
                }
                VertexGroups[index] = group;
                string wedgeKey = BuildWedgeKey(index, group);
                if (!identicalWedges.TryGetValue(wedgeKey, out int canonical)) {
                    canonical = index;
                    identicalWedges.Add(wedgeKey, canonical);
                }
                canonicalVertices[index] = canonical;
            }
            SubmeshTriangles = new int[asset.Submeshes == null || asset.Submeshes.Length == 0 ? 1 : asset.Submeshes.Length];
            for (int offset = 0; offset < indices.IndexCount; offset += 3) {
                ModelReductionTriangle triangle = new ModelReductionTriangle { Submesh = ResolveSubmesh(offset) };
                for (int corner = 0; corner < 3; corner++) {
                    uint vertex = indices.Uses32BitIndices ? indices.Indices32[offset + corner] : indices.Indices16[offset + corner];
                    if (vertex >= asset.Positions.Length) throw new InvalidOperationException("GPU reduction index exceeds the position buffer.");
                    triangle.Vertices[corner] = canonicalVertices[vertex];
                }
                int face = Triangles.Count;
                Triangles.Add(triangle);
                if (IsDegenerate(triangle.Vertices)) { triangle.Active = false; continue; }
                AddFace(face);
                TriangleCount++;
                SubmeshTriangles[triangle.Submesh]++;
                AddFaceQuadrics(triangle);
            }
            foreach (ModelReductionGroup group in Groups) VertexCount += group.Vertices.Count;
            InitializeExtrema();
        }

        /// <summary>Welds only bit-identical UV/normal wedges; distinct seams remain separate vertices.</summary>
        string BuildWedgeKey(int vertex, int group) {
            List<int> values = new List<int> { group };
            if (Asset.Normals != null && Asset.Normals.Length > 0) {
                float3 normal = Asset.Normals[vertex];
                if (!float.IsFinite(normal.X) || !float.IsFinite(normal.Y) || !float.IsFinite(normal.Z)) throw new InvalidOperationException("GPU reduction normals must be finite.");
                values.Add(BitConverter.SingleToInt32Bits(normal.X));
                values.Add(BitConverter.SingleToInt32Bits(normal.Y));
                values.Add(BitConverter.SingleToInt32Bits(normal.Z));
            }
            if (Asset.TexCoords != null && Asset.TexCoords.Length > 0) {
                float2 uv = Asset.TexCoords[vertex];
                if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y)) throw new InvalidOperationException("GPU reduction UVs must be finite.");
                values.Add(BitConverter.SingleToInt32Bits(uv.X));
                values.Add(BitConverter.SingleToInt32Bits(uv.Y));
            }
            return string.Join(",", values);
        }

        /// <summary>Collapses legal lowest-error edges until both caps hold, then commits compacted buffers.</summary>
        public void Reduce() {
            for (int group = 0; group < Groups.Count; group++) EnqueueNeighborhood(group);
            while (VertexCount > Settings.MaximumVertices || TriangleCount * 3 > Settings.MaximumIndices) {
                if (!Candidates.TryDequeue(out ModelReductionCandidate candidate, out _)) {
                    throw new InvalidOperationException($"GPU model '{Asset.Id}' cannot meet {Settings.MaximumVertices} vertices/{Settings.MaximumIndices} indices while preserving UV/normal/material seams, bounds and winding. Remaining={VertexCount} vertices/{TriangleCount * 3} indices.");
                }
                if (candidate.SourceRevision != Groups[candidate.Source].Revision || candidate.TargetRevision != Groups[candidate.Target].Revision) continue;
                Collapse(candidate);
            }
            if (TriangleCount == 0) throw new InvalidOperationException("GPU reduction cannot produce an empty surface.");
            Commit();
        }

        /// <summary>Resolves exact authored submesh coverage and rejects ambiguous or uncovered triangles.</summary>
        int ResolveSubmesh(int offset) {
            if (Asset.Submeshes == null || Asset.Submeshes.Length == 0) return 0;
            int result = -1;
            for (int index = 0; index < Asset.Submeshes.Length; index++) {
                ModelSubmeshAsset submesh = Asset.Submeshes[index];
                if (submesh == null || submesh.IndexStart < 0 || submesh.IndexCount < 0 || submesh.IndexStart % 3 != 0 || submesh.IndexCount % 3 != 0) throw new InvalidOperationException("GPU reduction requires valid triangle-aligned material ranges.");
                if (offset >= submesh.IndexStart && offset < submesh.IndexStart + submesh.IndexCount) {
                    if (result >= 0) throw new InvalidOperationException("GPU reduction material ranges overlap.");
                    result = index;
                }
            }
            if (result < 0) throw new InvalidOperationException("GPU reduction triangle has no authored material range.");
            return result;
        }

        /// <summary>Adds a triangle to its geometric groups and active wedge sets.</summary>
        void AddFace(int face) {
            foreach (int vertex in Triangles[face].Vertices) {
                ModelReductionGroup group = Groups[VertexGroups[vertex]];
                group.Faces.Add(face);
                group.Vertices.Add(vertex);
            }
        }

        /// <summary>Computes double-precision unnormalized triangle normal.</summary>
        double[] Normal(int[] vertices) {
            float3 a = Asset.Positions[vertices[0]], b = Asset.Positions[vertices[1]], c = Asset.Positions[vertices[2]];
            double x1 = (double)b.X - a.X, y1 = (double)b.Y - a.Y, z1 = (double)b.Z - a.Z;
            double x2 = (double)c.X - a.X, y2 = (double)c.Y - a.Y, z2 = (double)c.Z - a.Z;
            return new[] { y1 * z2 - z1 * y2, z1 * x2 - x1 * z2, x1 * y2 - y1 * x2 };
        }

        /// <summary>Detects zero-area triangles without discarding a valid finite-sized surface.</summary>
        bool IsDegenerate(int[] vertices) {
            double[] normal = Normal(vertices);
            return normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2] <= 1e-24d;
        }

        /// <summary>Accumulates area-weighted geometric error for an original source face.</summary>
        void AddFaceQuadrics(ModelReductionTriangle triangle) {
            double[] normal = Normal(triangle.Vertices);
            double length = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);
            float3 position = Asset.Positions[triangle.Vertices[0]];
            double x = normal[0] / length, y = normal[1] / length, z = normal[2] / length;
            double distance = -(x * position.X + y * position.Y + z * position.Z);
            foreach (int vertex in triangle.Vertices) Groups[VertexGroups[vertex]].Quadric.AddPlane(x, y, z, distance, length);
        }

        /// <summary>Reads one axis from a geometric position.</summary>
        static double Coordinate(float3 position, int axis) {
            if (axis == 0) return position.X;
            if (axis == 1) return position.Y;
            return position.Z;
        }

        /// <summary>Counts source bounds witnesses so planar surfaces remain reducible without losing extrema.</summary>
        void InitializeExtrema() {
            for (int axis = 0; axis < 3; axis++) {
                Extrema[axis * 2] = double.PositiveInfinity;
                Extrema[axis * 2 + 1] = double.NegativeInfinity;
                foreach (ModelReductionGroup group in Groups) {
                    if (group.Faces.Count == 0) continue;
                    double value = Coordinate(group.Position, axis);
                    Extrema[axis * 2] = Math.Min(Extrema[axis * 2], value);
                    Extrema[axis * 2 + 1] = Math.Max(Extrema[axis * 2 + 1], value);
                }
            }
            foreach (ModelReductionGroup group in Groups) {
                if (group.Faces.Count == 0) continue;
                for (int extremum = 0; extremum < 6; extremum++) {
                    if (Coordinate(group.Position, extremum / 2) == Extrema[extremum]) ExtremumCounts[extremum]++;
                }
            }
        }

        /// <summary>Queues directed edge candidates in stable order around one changed position.</summary>
        void EnqueueNeighborhood(int source) {
            if (Groups[source].Faces.Count == 0) return;
            SortedSet<int> neighbors = new SortedSet<int>();
            foreach (int face in Groups[source].Faces) {
                foreach (int vertex in Triangles[face].Vertices) {
                    int neighbor = VertexGroups[vertex];
                    if (neighbor != source) neighbors.Add(neighbor);
                }
            }
            foreach (int target in neighbors) {
                ModelReductionCandidate candidate = ValidateCandidate(source, target);
                if (candidate == null) continue;
                double error = Groups[source].Quadric.Evaluate(Groups[target].Position) + Groups[target].Quadric.Evaluate(Groups[target].Position);
                Candidates.Enqueue(candidate, new ModelReductionPriority(error, source, target));
            }
        }

        /// <summary>Pairs all seam wedges, checks material ownership and verifies surviving face orientation.</summary>
        ModelReductionCandidate ValidateCandidate(int source, int target) {
            ModelReductionGroup from = Groups[source], to = Groups[target];
            if (from.Faces.Count == 0 || to.Faces.Count == 0) return null;
            HashSet<int> edgeFaces = from.Faces.Where(to.Faces.Contains).ToHashSet();
            if (edgeFaces.Count == 0 || edgeFaces.Count > 2 || from.Faces.Count == edgeFaces.Count && to.Faces.Count == edgeFaces.Count) return null;
            HashSet<int> fromNeighbors = GetNeighbors(source), toNeighbors = GetNeighbors(target);
            fromNeighbors.IntersectWith(toNeighbors);
            HashSet<int> opposite = edgeFaces.SelectMany(face => Triangles[face].Vertices).Select(vertex => VertexGroups[vertex]).Where(group => group != source && group != target).ToHashSet();
            if (!fromNeighbors.SetEquals(opposite)) return null;
            for (int extremum = 0; extremum < 6; extremum++) {
                if (Coordinate(from.Position, extremum / 2) == Extrema[extremum] && ExtremumCounts[extremum] == 1 && Coordinate(to.Position, extremum / 2) != Extrema[extremum]) return null;
            }
            ModelReductionCandidate result = new ModelReductionCandidate { Source = source, Target = target, SourceRevision = from.Revision, TargetRevision = to.Revision };
            foreach (int face in from.Faces) {
                ModelReductionTriangle triangle = Triangles[face];
                int sourceVertex = -1, targetVertex = -1;
                foreach (int vertex in triangle.Vertices) {
                    if (VertexGroups[vertex] == source) sourceVertex = vertex;
                    if (VertexGroups[vertex] == target) targetVertex = vertex;
                }
                if (targetVertex < 0) continue;
                if (result.Wedges.TryGetValue(sourceVertex, out int existing) && existing != targetVertex) return null;
                result.Wedges[sourceVertex] = targetVertex;
            }
            if (result.Wedges.Count != from.Vertices.Count || result.Wedges.Values.Distinct().Count() != result.Wedges.Count) return null;
            foreach (KeyValuePair<int, int> wedge in result.Wedges) {
                HashSet<int> sourceMaterials = GetVertexMaterials(wedge.Key, from);
                if (!sourceMaterials.SetEquals(GetVertexMaterials(wedge.Value, to))) return null;
            }
            int[] removed = new int[SubmeshTriangles.Length];
            HashSet<int> removedFaces = new HashSet<int>();
            foreach (int face in from.Faces) {
                ModelReductionTriangle triangle = Triangles[face];
                int[] replaced = triangle.Vertices.Select(vertex => result.Wedges.TryGetValue(vertex, out int replacement) ? replacement : vertex).ToArray();
                if (replaced.Select(vertex => VertexGroups[vertex]).Distinct().Count() < 3) { removed[triangle.Submesh]++; removedFaces.Add(face); continue; }
                if (IsDegenerate(replaced)) return null;
                int[] replacedGroups = replaced.Select(vertex => VertexGroups[vertex]).OrderBy(group => group).ToArray();
                foreach (int targetFace in to.Faces) {
                    if (edgeFaces.Contains(targetFace)) continue;
                    if (Triangles[targetFace].Vertices.Select(vertex => VertexGroups[vertex]).OrderBy(group => group).SequenceEqual(replacedGroups)) return null;
                }
                double[] oldNormal = Normal(triangle.Vertices), newNormal = Normal(replaced);
                double dot = oldNormal[0] * newNormal[0] + oldNormal[1] * newNormal[1] + oldNormal[2] * newNormal[2];
                if (dot <= 0d) return null;
                if (Asset.TexCoords != null && Asset.TexCoords.Length > 0) {
                    double oldUvArea = UvArea(triangle.Vertices), newUvArea = UvArea(replaced);
                    if (oldUvArea != 0d && oldUvArea * newUvArea <= 0d) return null;
                }
            }
            if (removed.Sum() == 0) return null;
            for (int submesh = 0; submesh < removed.Length; submesh++) {
                if (removed[submesh] > 0 && removed[submesh] >= SubmeshTriangles[submesh]) return null;
            }
            HashSet<int> disappearing = new HashSet<int> { source };
            foreach (int face in removedFaces) {
                foreach (int vertex in Triangles[face].Vertices) {
                    int group = VertexGroups[vertex];
                    if (group != target && Groups[group].Faces.All(removedFaces.Contains)) disappearing.Add(group);
                }
            }
            for (int extremum = 0; extremum < 6; extremum++) {
                int lostWitnesses = disappearing.Count(group => Coordinate(Groups[group].Position, extremum / 2) == Extrema[extremum]);
                if (lostWitnesses >= ExtremumCounts[extremum]) return null;
            }
            return result;
        }

        /// <summary>Returns the geometric link used to prevent topology-changing edge collapses.</summary>
        HashSet<int> GetNeighbors(int group) {
            return Groups[group].Faces.SelectMany(face => Triangles[face].Vertices).Select(vertex => VertexGroups[vertex]).Where(neighbor => neighbor != group).ToHashSet();
        }

        /// <summary>Computes oriented texture area so a geometric collapse cannot invert its texture chart.</summary>
        double UvArea(int[] vertices) {
            float2 a = Asset.TexCoords[vertices[0]], b = Asset.TexCoords[vertices[1]], c = Asset.TexCoords[vertices[2]];
            return ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
        }

        /// <summary>Finds the exact material set of one attribute wedge without merging hard seams.</summary>
        HashSet<int> GetVertexMaterials(int vertex, ModelReductionGroup group) {
            HashSet<int> materials = new HashSet<int>();
            foreach (int face in group.Faces) {
                if (Triangles[face].Vertices.Contains(vertex)) materials.Add(Triangles[face].Submesh);
            }
            return materials;
        }

        /// <summary>Applies one legal endpoint collapse and invalidates only its affected neighborhood.</summary>
        void Collapse(ModelReductionCandidate candidate) {
            ModelReductionGroup from = Groups[candidate.Source], to = Groups[candidate.Target];
            int[] faces = from.Faces.OrderBy(face => face).ToArray();
            SortedSet<int> changed = new SortedSet<int> { candidate.Source, candidate.Target };
            foreach (int face in faces) {
                ModelReductionTriangle triangle = Triangles[face];
                foreach (int vertex in triangle.Vertices) {
                    int group = VertexGroups[vertex];
                    changed.Add(group);
                    Groups[group].Faces.Remove(face);
                }
                for (int corner = 0; corner < 3; corner++) {
                    if (candidate.Wedges.TryGetValue(triangle.Vertices[corner], out int replacement)) triangle.Vertices[corner] = replacement;
                }
                if (triangle.Vertices.Select(vertex => VertexGroups[vertex]).Distinct().Count() < 3) {
                    triangle.Active = false;
                    TriangleCount--;
                    SubmeshTriangles[triangle.Submesh]--;
                } else {
                    AddFace(face);
                }
            }
            to.Quadric.Add(from.Quadric);
            foreach (int groupIndex in changed) {
                ModelReductionGroup group = Groups[groupIndex];
                bool wasActive = group.Vertices.Count > 0;
                VertexCount -= group.Vertices.Count;
                group.Vertices.Clear();
                foreach (int face in group.Faces) {
                    foreach (int vertex in Triangles[face].Vertices) {
                        if (VertexGroups[vertex] == groupIndex) group.Vertices.Add(vertex);
                    }
                }
                VertexCount += group.Vertices.Count;
                if (wasActive && group.Vertices.Count == 0) {
                    for (int extremum = 0; extremum < 6; extremum++) {
                        if (Coordinate(group.Position, extremum / 2) == Extrema[extremum]) ExtremumCounts[extremum]--;
                    }
                }
                group.Revision++;
            }
            foreach (int group in changed) EnqueueNeighborhood(group);
        }

        /// <summary>Creates compact buffers retaining original attributes, triangle order and material slots.</summary>
        void Commit() {
            SortedSet<int> vertices = new SortedSet<int>(Triangles.Where(triangle => triangle.Active).SelectMany(triangle => triangle.Vertices));
            Dictionary<int, int> remap = new Dictionary<int, int>();
            int next = 0;
            foreach (int vertex in vertices) remap.Add(vertex, next++);
            float3[] positions = vertices.Select(vertex => Asset.Positions[vertex]).ToArray();
            float3[] normals = Asset.Normals == null || Asset.Normals.Length == 0 ? Asset.Normals : vertices.Select(vertex => Asset.Normals[vertex]).ToArray();
            float2[] uvs = Asset.TexCoords == null || Asset.TexCoords.Length == 0 ? Asset.TexCoords : vertices.Select(vertex => Asset.TexCoords[vertex]).ToArray();
            List<ushort> indices = new List<ushort>();
            List<ModelSubmeshAsset> submeshes = new List<ModelSubmeshAsset>();
            for (int submesh = 0; submesh < SubmeshTriangles.Length; submesh++) {
                int start = indices.Count;
                foreach (ModelReductionTriangle triangle in Triangles) {
                    if (triangle.Active && triangle.Submesh == submesh) {
                        foreach (int vertex in triangle.Vertices) indices.Add(checked((ushort)remap[vertex]));
                    }
                }
                if (Asset.Submeshes != null && Asset.Submeshes.Length > 0) submeshes.Add(new ModelSubmeshAsset { MaterialSlotName = Asset.Submeshes[submesh].MaterialSlotName, IndexStart = start, IndexCount = indices.Count - start });
            }
            if (positions.Length > Settings.MaximumVertices || indices.Count > Settings.MaximumIndices) throw new InvalidOperationException("GPU reduction output exceeded the validated caps.");
            for (int axis = 0; axis < 3; axis++) {
                if (positions.Min(position => Coordinate(position, axis)) != Extrema[axis * 2] || positions.Max(position => Coordinate(position, axis)) != Extrema[axis * 2 + 1]) throw new InvalidOperationException("GPU reduction failed to preserve source bounds.");
            }
            Asset.Positions = positions;
            Asset.Normals = normals;
            Asset.TexCoords = uvs;
            Asset.Indices16 = indices.ToArray();
            Asset.Indices32 = null;
            if (Asset.Submeshes != null && Asset.Submeshes.Length > 0) Asset.Submeshes = submeshes.ToArray();
        }
    }
}
