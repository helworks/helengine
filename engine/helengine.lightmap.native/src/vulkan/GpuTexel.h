#pragma once
#include <cstdint>
namespace hellm::detail {
/// <summary>Explicit std430 layout shared with the Vulkan irradiance shader.</summary>
struct alignas(16) GpuTexel {
    /// <summary>First coverage, coverage count, original atlas pixel and reserved word.</summary>
    uint32_t Metadata[4];
};
static_assert(sizeof(GpuTexel) == 16, "Shader layout mismatch");
} // namespace hellm::detail
