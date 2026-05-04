using System.Text.Json;
using helengine.baseplatform.Manifest;

namespace helengine.files {
    /// <summary>
    /// Writes a segmented packfile layout as a future-friendly mock container representation.
    /// </summary>
    public sealed class SegmentedPackfileContainerWriter : IPlatformContainerWriter {
        readonly PackfileWritePlan WritePlan;

        /// <summary>
        /// Initializes one segmented packfile writer for the supplied plan.
        /// </summary>
        public SegmentedPackfileContainerWriter(PackfileWritePlan writePlan) {
            WritePlan = writePlan ?? throw new ArgumentNullException(nameof(writePlan));
        }

        /// <summary>
        /// Writes the supplied manifest into one or more segment manifests.
        /// </summary>
        public void Write(PlatformBuildManifest manifest, string sourceRootPath, string outputRootPath) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }
            if (string.IsNullOrWhiteSpace(sourceRootPath)) {
                throw new ArgumentException("Source root path must be provided.", nameof(sourceRootPath));
            }
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            Directory.CreateDirectory(outputRootPath);

            List<PackfileSegmentEntry> entries = [];
            if (Directory.Exists(sourceRootPath)) {
                string[] sourceFiles = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
                for (int index = 0; index < sourceFiles.Length; index++) {
                    string sourceFilePath = sourceFiles[index];
                    string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath).Replace('\\', '/');
                    entries.Add(new PackfileSegmentEntry(relativePath, new FileInfo(sourceFilePath).Length));
                }

                entries.Sort(CompareEntries);
            }

            List<List<PackfileSegmentEntry>> segments = SplitIntoSegments(entries);
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
                PackfileSegmentManifest segmentManifest = new(
                    WritePlan.ContainerId,
                    segmentIndex,
                    WritePlan.MaxSegmentSizeBytes,
                    manifest.ContainerWritePlan.RuntimeSpecializationId,
                    [.. segments[segmentIndex]]);

                string segmentFilePath = Path.Combine(outputRootPath, $"{WritePlan.ContainerId}.segment-{segmentIndex:000}.pack.json");
                File.WriteAllText(segmentFilePath, JsonSerializer.Serialize(segmentManifest, JsonOptions));
            }
        }

        List<List<PackfileSegmentEntry>> SplitIntoSegments(IReadOnlyList<PackfileSegmentEntry> entries) {
            if (entries.Count == 0) {
                return [new List<PackfileSegmentEntry>()];
            }

            if (WritePlan.MaxSegmentSizeBytes <= 0) {
                return [new List<PackfileSegmentEntry>(entries)];
            }

            List<List<PackfileSegmentEntry>> segments = [];
            List<PackfileSegmentEntry> currentSegment = new();
            long currentSize = 0;

            for (int index = 0; index < entries.Count; index++) {
                PackfileSegmentEntry entry = entries[index];
                bool wouldOverflow = currentSegment.Count > 0 && currentSize + entry.LengthBytes > WritePlan.MaxSegmentSizeBytes;
                if (wouldOverflow) {
                    segments.Add(currentSegment);
                    currentSegment = new List<PackfileSegmentEntry>();
                    currentSize = 0;
                }

                currentSegment.Add(entry);
                currentSize += entry.LengthBytes;
            }

            if (currentSegment.Count > 0 || segments.Count == 0) {
                segments.Add(currentSegment);
            }

            return segments;
        }

        /// <summary>
        /// Orders packfile entries so root-level files remain stable ahead of nested content before path-level comparison.
        /// </summary>
        /// <param name="left">The left entry being compared.</param>
        /// <param name="right">The right entry being compared.</param>
        /// <returns>Comparison result suitable for stable entry sorting.</returns>
        static int CompareEntries(PackfileSegmentEntry left, PackfileSegmentEntry right) {
            if (left == null) {
                return right == null ? 0 : -1;
            }

            if (right == null) {
                return 1;
            }

            int leftDepth = CountPathDepth(left.RelativePath);
            int rightDepth = CountPathDepth(right.RelativePath);
            int depthComparison = leftDepth.CompareTo(rightDepth);
            if (depthComparison != 0) {
                return depthComparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
        }

        /// <summary>
        /// Counts path separators so shallower entries can be emitted before nested entries.
        /// </summary>
        /// <param name="relativePath">Relative entry path to inspect.</param>
        /// <returns>The number of directory separators in the relative path.</returns>
        static int CountPathDepth(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return 0;
            }

            int depth = 0;
            for (int index = 0; index < relativePath.Length; index++) {
                if (relativePath[index] == '/') {
                    depth++;
                }
            }

            return depth;
        }

        sealed class PackfileSegmentManifest {
            public PackfileSegmentManifest(string containerId, int segmentIndex, long maxSegmentSizeBytes, string runtimeSpecializationId, PackfileSegmentEntry[] entries) {
                ContainerId = containerId;
                SegmentIndex = segmentIndex;
                MaxSegmentSizeBytes = maxSegmentSizeBytes;
                RuntimeSpecializationId = runtimeSpecializationId;
                Entries = entries ?? [];
            }

            public string ContainerId { get; }
            public int SegmentIndex { get; }
            public long MaxSegmentSizeBytes { get; }
            public string RuntimeSpecializationId { get; }
            public PackfileSegmentEntry[] Entries { get; }
        }

        sealed class PackfileSegmentEntry {
            public PackfileSegmentEntry(string relativePath, long lengthBytes) {
                RelativePath = relativePath;
                LengthBytes = lengthBytes;
            }

            public string RelativePath { get; }
            public long LengthBytes { get; }
        }

        static readonly JsonSerializerOptions JsonOptions = new() {
            WriteIndented = true
        };
    }
}
