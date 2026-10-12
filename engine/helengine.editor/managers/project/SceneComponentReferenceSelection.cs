namespace helengine.editor {
    /// <summary>Tracks authored component alternatives and their effective packaged references without discarding explicit scene-only dependencies.</summary>
    sealed class SceneComponentReferenceSelection {
        /// <summary>References found in component base payloads or scoped override metadata.</summary>
        readonly HashSet<string> Candidates = new(StringComparer.Ordinal);
        /// <summary>Final references selected by any component, including multiple cooked variants of one authored asset.</summary>
        readonly Dictionary<string, List<SceneAssetReference>> Selected = new(StringComparer.Ordinal);
        /// <summary>Typed outputs that may subsequently be replaced by component-specific processing.</summary>
        readonly HashSet<string> IntermediatePaths = new(StringComparer.Ordinal);
        /// <summary>Exact runtime paths retained by any effective member or explicit table-only reference.</summary>
        readonly HashSet<string> FinalPaths = new(StringComparer.Ordinal);

        /// <summary>Starts an independent selection for the next scene after global asset deduplication.</summary>
        public void Clear() { Candidates.Clear(); Selected.Clear(); IntermediatePaths.Clear(); FinalPaths.Clear(); }

        /// <summary>Records a typed rewrite output before subsequent mesh processing can replace it.</summary>
        /// <param name="reference">Intermediate reference produced by the typed asset writer.</param>
        public void RememberIntermediate(SceneAssetReference reference) {
            if (reference != null && !string.IsNullOrWhiteSpace(reference.RelativePath)) IntermediatePaths.Add(reference.RelativePath.TrimStart('/'));
        }

        /// <summary>Lists exact references retained by this scene, including globally deduplicated assets that did not trigger another file write.</summary>
        /// <returns>Final component and explicit scene-table paths required by this scene's preload closure.</returns>
        public IEnumerable<string> GetFinalPaths() { return FinalPaths; }

        /// <summary>Lists only replaced outputs that no effective member or independent scene dependency still uses.</summary>
        /// <returns>Intermediate paths safe to omit from this scene's streaming closure.</returns>
        public IEnumerable<string> GetUnusedIntermediatePaths() { return IntermediatePaths.Where(path => !FinalPaths.Contains(path)); }

        /// <summary>Records an authored reference that is eligible for component override selection.</summary>
        /// <param name="reference">Reference read from a component payload or override.</param>
        public void RememberCandidate(SceneAssetReference reference) {
            if (reference != null) Candidates.Add(BuildKey(reference));
        }

        /// <summary>Records one effective component reference after every typed and variant rewrite has completed.</summary>
        /// <param name="source">Authored reference selected by the component.</param>
        /// <param name="packaged">Final reference serialized into that component.</param>
        public void RememberSelected(SceneAssetReference source, SceneAssetReference packaged) {
            if (source == null || packaged == null) return;
            if (!string.IsNullOrWhiteSpace(packaged.RelativePath)) FinalPaths.Add(packaged.RelativePath.TrimStart('/'));
            string key = BuildKey(source);
            Candidates.Add(key);
            if (!Selected.TryGetValue(key, out List<SceneAssetReference> references)) {
                references = new List<SceneAssetReference>();
                Selected.Add(key, references);
            }
            string packagedKey = BuildKey(packaged);
            if (!references.Any(reference => BuildKey(reference) == packagedKey)) references.Add(packaged);
        }

        /// <summary>Distinguishes effective component dependencies from inactive alternatives and independent scene-table references.</summary>
        /// <param name="source">Original scene-table reference.</param>
        /// <param name="references">Selected final references, or an empty array for an inactive alternative.</param>
        /// <returns>True only when the reference participates in component selection.</returns>
        public bool TryGetSelection(SceneAssetReference source, out SceneAssetReference[] references) {
            references = Array.Empty<SceneAssetReference>();
            if (source == null) return false;
            string key = BuildKey(source);
            if (Selected.TryGetValue(key, out List<SceneAssetReference> selected)) {
                references = selected.ToArray();
                return true;
            }
            return Candidates.Contains(key);
        }

        /// <summary>Identifies the same authored reference across the component payload and scene table, preserving generated-provider identity.</summary>
        /// <param name="reference">Authored or packaged reference.</param>
        /// <returns>Canonical source-kind, provider, asset-id and relative-path identity.</returns>
        static string BuildKey(SceneAssetReference reference) {
            string path = string.IsNullOrWhiteSpace(reference.RelativePath) ? string.Empty : CanonicalPackagedAssetPath.Normalize(reference.RelativePath.TrimStart('/'));
            return string.Concat((int)reference.SourceKind, "\n", reference.ProviderId, "\n", reference.AssetId, "\n", path);
        }
    }
}
