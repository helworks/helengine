#include "vulkan_indirect_range_plan.hpp"
#include <array>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Checks exact direct fallback interval merging without closing gaps, reordering or accepting non-triangles.</summary>
int main(){
 const std::array<VulkanIndirectRange,2> contiguous{{{0,3},{3,6}}},gapped{{{0,3},{6,3}}},reversed{{{3,3},{0,3}}},invalid{{{0,2},{2,3}}};
 if(VulkanIndirectRangePlan::ContiguousIndexCount(contiguous)!=9 || VulkanIndirectRangePlan::ContiguousIndexCount(gapped)!=0 || VulkanIndirectRangePlan::ContiguousIndexCount(reversed)!=0 || VulkanIndirectRangePlan::ContiguousIndexCount(invalid)!=0)throw std::runtime_error("Direct range coalescing changed order/gaps/triangles.");
}
