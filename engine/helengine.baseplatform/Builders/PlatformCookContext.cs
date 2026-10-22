using System.Text.Json;

namespace helengine.baseplatform.Builders {
    /// <summary>Owns the immutable platform and native SDK snapshot shared by shader cooking, cache lookup and native packaging.</summary>
    public sealed class PlatformCookContext {
        /// <summary>Creates an explicit context; a platform without an SDK-dependent provider carries no native SDK.</summary>
        public PlatformCookContext(string platformId, PlatformNativeSdkSelection nativeSdk = null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(platformId);
            PlatformId = platformId;
            NativeSdk = nativeSdk;
            CacheIdentity = JsonSerializer.Serialize(new { Version = 1, PlatformId, NativeSdk });
        }
        /// <summary>Identifies the canonical target whose builder captured this context.</summary>
        public string PlatformId { get; }
        /// <summary>Retains the immutable native SDK selection when compiled payloads depend on it.</summary>
        public PlatformNativeSdkSelection NativeSdk { get; }
        /// <summary>Returns a canonical, unambiguous identity derived once from every captured toolchain field.</summary>
        public string CacheIdentity { get; }
    }
}
