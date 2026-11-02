#pragma once
#include <cstdint>
namespace hellm::detail {
/// <summary>Explicit std430 layout shared with the Vulkan irradiance shader.</summary>
struct alignas(16) GpuMaterial {
    /// <summary>Linear diffuse albedo.</summary>
    float Albedo[4];
    /// <summary>Metallic fraction and perceptual GGX roughness in xy; remaining lanes reserved.</summary>
    float Surface[4];
    /// <summary>Texture width, height, pixel offset and reserved word.</summary>
    uint32_t Texture[4];
};
static_assert(sizeof(GpuMaterial) == 48, "Shader layout mismatch");
} // namespace hellm::detail
