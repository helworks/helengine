#pragma once
#include <cstdint>
namespace hellm::detail {
/// <summary>Explicit std430 layout shared with the Vulkan irradiance shader.</summary>
struct alignas(16) GpuParameters {
    /// <summary>Normalized sun direction and world-space ray epsilon.</summary>
    float Sun[4];
    /// <summary>Normal-incidence irradiance in xyz; one minus the cap half-angle cosine in w, zero for directional sun.</summary>
    float Radiance[4];
    /// <summary>Sample count, bounce limit, batch texel count and positive-angular-diameter flag.</summary>
    uint32_t Settings[4];
    /// <summary>Low and high deterministic seed words, explicit mode (zero irradiance, one reflection radiance), and a reserved word.</summary>
    uint32_t Seed[4];
};
static_assert(sizeof(GpuParameters) == 64, "Shader layout mismatch");
} // namespace hellm::detail
