using System.Text;

namespace helengine {
    /// <summary>
    /// Creates stable cache and device-job identities from every shader compilation input.
    /// </summary>
    public static class ShaderCompileRequestIdentity {
        /// <summary>
        /// Builds the cache key used by the shared shader compile service.
        /// </summary>
        /// <param name="request">Shader compilation request to identify.</param>
        /// <param name="sourceHasher">Hasher used to identify complete source text.</param>
        /// <returns>Stable cache key for the request.</returns>
        public static ShaderCompileCacheKey CreateCacheKey(ShaderCompileRequest request, ShaderSourceHasher sourceHasher) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            } else if (sourceHasher == null) {
                throw new ArgumentNullException(nameof(sourceHasher));
            }

            return new ShaderCompileCacheKey(
                sourceHasher.ComputeHash(request.Source.Source),
                request.ProgramName,
                request.EntryPoint,
                request.Stage,
                request.Target,
                request.ShaderModel,
                request.Variant,
                BuildDefinesSignature(request.Defines),
                BuildBindingPolicySignature(request.Options.BindingPolicy));
        }

        /// <summary>
        /// Builds the canonical define-list signature used by cache and device-job identities.
        /// </summary>
        /// <param name="defines">Defines in their deterministic request order.</param>
        /// <returns>Canonical define signature.</returns>
        static string BuildDefinesSignature(IReadOnlyList<ShaderDefine> defines) {
            if (defines.Count == 0) {
                return string.Empty;
            }

            StringBuilder builder = new();
            for (int index = 0; index < defines.Count; index++) {
                ShaderDefine define = defines[index];
                builder.Append(define.Name);
                builder.Append('=');
                builder.Append(define.Value);
                builder.Append(';');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Builds the canonical resource-binding policy signature used by cache and device-job identities.
        /// </summary>
        /// <param name="policy">Binding policy to identify.</param>
        /// <returns>Canonical binding policy signature.</returns>
        static string BuildBindingPolicySignature(ShaderBindingPolicy policy) {
            return string.Concat(
                policy.DefaultSpace.ToString(),
                ":b", policy.ConstantBufferShift.ToString(),
                ":t", policy.TextureShift.ToString(),
                ":s", policy.SamplerShift.ToString(),
                ":u", policy.StorageShift.ToString());
        }
    }
}
