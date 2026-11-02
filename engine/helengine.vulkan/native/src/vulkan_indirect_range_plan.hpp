#pragma once
#include "vulkan_indirect_range.hpp"
#include <span>
namespace helengine::vulkan {
/// <summary>Recognizes an exactly contiguous ordered triangle interval without closing gaps or reordering ranges.</summary>
class VulkanIndirectRangePlan final {
public:
    /// <summary>Returns a combined uint32 triangle count, or zero when ranges require separate direct commands.</summary>
    static uint32_t ContiguousIndexCount(std::span<const VulkanIndirectRange> ranges) {
        if(ranges.empty())return 0;
        uint64_t end=ranges.front().FirstIndex,total=0;
        for(const auto& range:ranges) {
            if(!range.IndexCount || range.IndexCount%3 || range.FirstIndex!=end)return 0;
            total+=range.IndexCount;end+=range.IndexCount;
            if(total>UINT32_MAX || end>UINT32_MAX)return 0;
        }
        return static_cast<uint32_t>(total);
    }
};
}
