#include "vulkan_indirect_bounds.hpp"
#include <cstring>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Checks exact uint metadata packing beyond float32 integer precision without allocating huge geometry.</summary>
int main(){
 VulkanIndirectBounds bounds{{-1.5f,2.0f,-3.0f},0xabcdef03u,{4.0f,5.0f,6.5f},0x12345678u};
 uint32_t count=0,first=0;std::memcpy(&count,reinterpret_cast<const unsigned char*>(&bounds)+12,4);std::memcpy(&first,reinterpret_cast<const unsigned char*>(&bounds)+28,4);
 if(count!=0xabcdef03u || first!=0x12345678u || sizeof(bounds)!=32 || bounds.Minimum[0]!=-1.5f || bounds.Maximum[2]!=6.5f)throw std::runtime_error("Indirect metadata was rounded or changed its std430 layout.");
}
