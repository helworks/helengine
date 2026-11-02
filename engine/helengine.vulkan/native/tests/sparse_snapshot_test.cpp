#include "vulkan_mesh_material_snapshot.hpp"
#include <stdexcept>
#include <iostream>
using namespace helengine::vulkan;
/// <summary>Proves lazy materialization preserves exact compatibility keys and submitted uniform bytes.</summary>
int main() {
 std::vector<VulkanMeshRenderer::BufferBinding> original{{0,std::vector<uint8_t>(224,1)},{1,std::vector<uint8_t>(288,2)},{3,{4,5,6,7}}};
 auto full=original;full[0].Bytes.assign(224,3);
 VulkanMeshMaterialSnapshot eager(8,9,true,original,full,{});
 VulkanMeshMaterialSnapshot sparse(8,9,true,original,{full[0]},{},true,true);
 if(sparse.State!=eager.State || sparse.CacheState!=eager.CacheState || sparse.GetPayloadBytes()>=eager.GetPayloadBytes()) throw std::runtime_error("Sparse snapshot changed exact effective state or retained duplicate payloads.");
 auto bindings=sparse.CreateBakedBindings();
 if(bindings.size()!=full.size()) throw std::runtime_error("Sparse baked bindings missing.");
 for(size_t i=0;i<full.size();++i) if(bindings[i].Slot!=full[i].Slot || bindings[i].Bytes!=full[i].Bytes) throw std::runtime_error("Lazy baked binding bytes differ.");
 std::cout<<"PASS lazy baked bindings preserve exact keys/bytes and reduce retained payloads\n";
}
