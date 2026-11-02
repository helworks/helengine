#pragma once
#include <cstdint>
namespace helengine::vulkan {
/// <summary>Identifies an ordered triangle range whose bounds are derived from actual uploaded vertices.</summary>
struct VulkanIndirectRange final {
    /// <summary>Indexes the first uint32 element of the immutable page index buffer.</summary>
    uint32_t FirstIndex;
    /// <summary>Counts triangle indices, never vertices or bytes.</summary>
    uint32_t IndexCount;
};
}
