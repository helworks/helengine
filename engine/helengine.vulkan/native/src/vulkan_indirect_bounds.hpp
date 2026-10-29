#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <type_traits>
namespace helengine::vulkan {
/// <summary>Stores immutable bounds and exact triangle metadata in one std430-compatible 32-byte GPU record.</summary>
struct alignas(16) VulkanIndirectBounds final {
    /// <summary>Stores the lower corner derived from actual referenced float32 vertices.</summary>
    std::array<float,3> Minimum;
    /// <summary>Preserves the original uint32 triangle index count without conversion through float padding.</summary>
    uint32_t IndexCount;
    /// <summary>Stores the upper corner derived from actual referenced float32 vertices.</summary>
    std::array<float,3> Maximum;
    /// <summary>Preserves the original uint32 first index without precision loss.</summary>
    uint32_t FirstIndex;
};
static_assert(sizeof(float)==4 && sizeof(uint32_t)==4,"Indirect metadata requires fixed 32-bit shader scalar widths.");
static_assert(std::is_standard_layout_v<VulkanIndirectBounds> && sizeof(VulkanIndirectBounds)==32,"Indirect bounds must preserve their std430 array stride.");
static_assert(offsetof(VulkanIndirectBounds,Minimum)==0 && offsetof(VulkanIndirectBounds,IndexCount)==12 && offsetof(VulkanIndirectBounds,Maximum)==16 && offsetof(VulkanIndirectBounds,FirstIndex)==28,"Indirect metadata offsets must match the compute storage contract.");
}
