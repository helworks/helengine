using helengine.baseplatform.Results;

namespace helengine.editor {
    /// <summary>Merges renderer-owned shader dependencies with material dependencies while preserving request order.</summary>
    public static class EditorRendererShaderDependencyResolver {
        /// <summary>Merges material dependencies before renderer declarations and validates renderer-reserved identities.</summary>
        /// <param name="materialDependencies">Dependencies reported by packaged materials.</param>
        /// <param name="rendererDependencies">Dependencies declared by the selected platform renderer.</param>
        /// <returns>First-request-ordered dependencies with exact ordinal identities deduplicated.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either dependency list is missing.</exception>
        /// <exception cref="InvalidOperationException">Thrown for null entries, incomplete renderer identities, or conflicting renderer reservations.</exception>
        public static IReadOnlyList<PlatformShaderDependency> Merge(
            IReadOnlyList<PlatformShaderDependency> materialDependencies,
            IReadOnlyList<PlatformShaderDependency> rendererDependencies) {
            if (materialDependencies == null) {
                throw new ArgumentNullException(nameof(materialDependencies));
            } else if (rendererDependencies == null) {
                throw new ArgumentNullException(nameof(rendererDependencies));
            }

            List<PlatformShaderDependency> merged = new(materialDependencies.Count + rendererDependencies.Count);
            HashSet<ShaderDependencyIdentity> identities = [];
            Dictionary<RendererShaderReservation, ShaderDependencyIdentity> rendererReservations = [];
            for (int index = 0; index < materialDependencies.Count; index++) {
                PlatformShaderDependency dependency = materialDependencies[index]
                    ?? throw new InvalidOperationException($"Material shader dependency at index {index} is null.");
                AddIfUnique(dependency, merged, identities);
            }

            for (int index = 0; index < rendererDependencies.Count; index++) {
                PlatformShaderDependency dependency = rendererDependencies[index]
                    ?? throw new InvalidOperationException($"Renderer shader dependency at index {index} is null.");
                if (!dependency.HasProgramPair) {
                    throw new InvalidOperationException($"Renderer shader dependency '{dependency.ShaderAssetId}' must select a complete program pair and variant.");
                }

                RendererShaderReservation reservation = new(dependency.ShaderAssetId, dependency.VariantName);
                ShaderDependencyIdentity identity = new(dependency);
                if (rendererReservations.TryGetValue(reservation, out ShaderDependencyIdentity reservedIdentity)
                    && !reservedIdentity.Equals(identity)) {
                    throw CreateReservationConflict(dependency);
                }

                rendererReservations[reservation] = identity;
                AddIfUnique(dependency, merged, identities);
            }

            foreach (KeyValuePair<RendererShaderReservation, ShaderDependencyIdentity> reservation in rendererReservations) {
                for (int index = 0; index < materialDependencies.Count; index++) {
                    PlatformShaderDependency materialDependency = materialDependencies[index];
                    if (materialDependency != null
                        && materialDependency.HasProgramPair
                        && reservation.Key.Matches(materialDependency)
                        && !reservation.Value.Equals(new ShaderDependencyIdentity(materialDependency))) {
                        throw CreateReservationConflict(materialDependency);
                    }
                }
            }

            return merged.AsReadOnly();
        }

        /// <summary>Adds a complete dependency only when its full ordinal identity has not appeared earlier.</summary>
        /// <param name="dependency">Dependency to append.</param>
        /// <param name="merged">Ordered result under construction.</param>
        /// <param name="identities">Full identities already appended.</param>
        static void AddIfUnique(
            PlatformShaderDependency dependency,
            List<PlatformShaderDependency> merged,
            HashSet<ShaderDependencyIdentity> identities) {
            if (!dependency.HasProgramPair || identities.Add(new ShaderDependencyIdentity(dependency))) {
                merged.Add(dependency);
            }
        }

        /// <summary>Creates an explicit error for two program pairs claiming one renderer asset and variant.</summary>
        /// <param name="dependency">Conflicting dependency identity.</param>
        /// <returns>Conflict exception with the affected stable identity.</returns>
        static InvalidOperationException CreateReservationConflict(PlatformShaderDependency dependency) {
            return new InvalidOperationException(
                $"Renderer shader dependency '{dependency.ShaderAssetId}' variant '{dependency.VariantName}' conflicts with a different vertex/pixel program pair.");
        }

        /// <summary>Stores the shader asset and variant pair reserved by one renderer declaration.</summary>
        readonly struct RendererShaderReservation : IEquatable<RendererShaderReservation> {
            /// <summary>Stores the exact shader asset identity.</summary>
            readonly string ShaderAssetId;
            /// <summary>Stores the exact renderer variant identity.</summary>
            readonly string VariantName;

            /// <summary>Creates a renderer reservation key.</summary>
            /// <param name="shaderAssetId">Exact shader asset identity.</param>
            /// <param name="variantName">Exact shader variant identity.</param>
            public RendererShaderReservation(string shaderAssetId, string variantName) {
                ShaderAssetId = shaderAssetId;
                VariantName = variantName;
            }

            /// <summary>Checks whether a complete dependency claims this asset and variant.</summary>
            /// <param name="dependency">Dependency to compare.</param>
            /// <returns>True when both ordinal identity fields match.</returns>
            public bool Matches(PlatformShaderDependency dependency) {
                return string.Equals(ShaderAssetId, dependency.ShaderAssetId, StringComparison.Ordinal)
                    && string.Equals(VariantName, dependency.VariantName, StringComparison.Ordinal);
            }

            /// <summary>Compares two renderer reservation keys using ordinal string identity.</summary>
            /// <param name="other">Other reservation key.</param>
            /// <returns>True when both identity fields match exactly.</returns>
            public bool Equals(RendererShaderReservation other) {
                return string.Equals(ShaderAssetId, other.ShaderAssetId, StringComparison.Ordinal)
                    && string.Equals(VariantName, other.VariantName, StringComparison.Ordinal);
            }

            /// <summary>Compares this reservation with another boxed value.</summary>
            /// <param name="obj">Object to compare.</param>
            /// <returns>True when the object contains the same reservation.</returns>
            public override bool Equals(object obj) => obj is RendererShaderReservation other && Equals(other);

            /// <summary>Creates a hash code consistent with ordinal reservation equality.</summary>
            /// <returns>Stable-in-process hash code for both identity fields.</returns>
            public override int GetHashCode() => HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(ShaderAssetId),
                StringComparer.Ordinal.GetHashCode(VariantName));
        }

        /// <summary>Stores the complete four-field shader dependency identity used for exact deduplication.</summary>
        readonly struct ShaderDependencyIdentity : IEquatable<ShaderDependencyIdentity> {
            /// <summary>Stores the exact shader asset identity.</summary>
            readonly string ShaderAssetId;
            /// <summary>Stores the exact vertex program identity.</summary>
            readonly string VertexProgramName;
            /// <summary>Stores the exact pixel program identity.</summary>
            readonly string PixelProgramName;
            /// <summary>Stores the exact variant identity.</summary>
            readonly string VariantName;

            /// <summary>Creates a full shader dependency identity.</summary>
            /// <param name="dependency">Dependency whose exact fields are captured.</param>
            public ShaderDependencyIdentity(PlatformShaderDependency dependency) {
                ShaderAssetId = dependency.ShaderAssetId;
                VertexProgramName = dependency.VertexProgramName;
                PixelProgramName = dependency.PixelProgramName;
                VariantName = dependency.VariantName;
            }

            /// <summary>Compares every dependency field using ordinal string identity.</summary>
            /// <param name="other">Other dependency identity.</param>
            /// <returns>True when all four fields match exactly.</returns>
            public bool Equals(ShaderDependencyIdentity other) {
                return string.Equals(ShaderAssetId, other.ShaderAssetId, StringComparison.Ordinal)
                    && string.Equals(VertexProgramName, other.VertexProgramName, StringComparison.Ordinal)
                    && string.Equals(PixelProgramName, other.PixelProgramName, StringComparison.Ordinal)
                    && string.Equals(VariantName, other.VariantName, StringComparison.Ordinal);
            }

            /// <summary>Compares this identity with another boxed value.</summary>
            /// <param name="obj">Object to compare.</param>
            /// <returns>True when the object contains the same dependency identity.</returns>
            public override bool Equals(object obj) => obj is ShaderDependencyIdentity other && Equals(other);

            /// <summary>Creates a hash code consistent with ordinal four-field identity equality.</summary>
            /// <returns>Stable-in-process hash code for all identity fields.</returns>
            public override int GetHashCode() => HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(ShaderAssetId),
                StringComparer.Ordinal.GetHashCode(VertexProgramName),
                StringComparer.Ordinal.GetHashCode(PixelProgramName),
                StringComparer.Ordinal.GetHashCode(VariantName));
        }
    }
}
