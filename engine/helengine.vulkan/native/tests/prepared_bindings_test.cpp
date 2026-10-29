#include "vulkan_prepared_bindings.hpp"
#include <iostream>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Fails the contract immediately without depending on release-build assertion settings.</summary>
void RequireTest(bool value,const char* message) {if(!value) throw std::runtime_error(message);}
/// <summary>Checks persistent GPU descriptors, changed uploads, schema invalidation and retirement epochs on a real validated device.</summary>
int main() {
 try {
  VulkanContext context({},true,VK_API_VERSION_1_2);
  VkDescriptorSetLayout layout=VK_NULL_HANDLE;VkSampler sampler=VK_NULL_HANDLE;VkSampler replacementSampler=VK_NULL_HANDLE;
  const std::array<VkDescriptorSetLayoutBinding,2> bindings={{{0,VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,1,VK_SHADER_STAGE_VERTEX_BIT,nullptr},{1,VK_DESCRIPTOR_TYPE_SAMPLER,1,VK_SHADER_STAGE_FRAGMENT_BIT,nullptr}}};
  VkDescriptorSetLayoutCreateInfo create{VK_STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO};create.bindingCount=bindings.size();create.pBindings=bindings.data();
  VulkanContext::Require(vkCreateDescriptorSetLayout(context.GetDevice(),&create,nullptr,&layout),"test layout");
  VkSamplerCreateInfo samplerCreate{VK_STRUCTURE_TYPE_SAMPLER_CREATE_INFO};samplerCreate.maxLod=1;
  VulkanContext::Require(vkCreateSampler(context.GetDevice(),&samplerCreate,nullptr,&sampler),"test sampler");
  VulkanContext::Require(vkCreateSampler(context.GetDevice(),&samplerCreate,nullptr,&replacementSampler),"test replacement sampler");
  {
   std::vector<VkDescriptorSetLayout> layouts{layout};std::vector<size_t> sizes{16};std::array<uint32_t,3> counts{1,0,1};
   VulkanPreparedBindings prepared(context,layouts,sizes,counts);
   std::array<uint8_t,16> bytes{};
   RequireTest(prepared.Matches(layouts,sizes),"same schema must reuse");
   RequireTest(prepared.WriteBuffer(0,0,0,bytes),"first uniform upload");
   RequireTest(!prepared.WriteBuffer(0,0,0,bytes),"unchanged uniform must not upload");
   bytes[7]=93;RequireTest(prepared.WriteBuffer(0,0,0,bytes),"changed uniform must upload");
   RequireTest(!prepared.WriteBuffer(0,0,0,bytes),"updated uniform must persist");
   VkDescriptorImageInfo image{sampler,VK_NULL_HANDLE,VK_IMAGE_LAYOUT_UNDEFINED};
   RequireTest(prepared.WriteImage(0,0,1,VK_DESCRIPTOR_TYPE_SAMPLER,image),"first sampler write");
   RequireTest(!prepared.WriteImage(0,0,1,VK_DESCRIPTOR_TYPE_SAMPLER,image),"unchanged sampler must not write");
   image.sampler=replacementSampler;
   RequireTest(prepared.WriteImage(0,0,1,VK_DESCRIPTOR_TYPE_SAMPLER,image),"changed sampler must write");
   RequireTest(!prepared.WriteImage(0,0,1,VK_DESCRIPTOR_TYPE_SAMPLER,image),"replacement sampler must persist");
   bool rejected=false;try{prepared.WriteBuffer(0,0,0,std::span<const uint8_t>(bytes).first(8));}catch(const std::invalid_argument&){rejected=true;}
   RequireTest(rejected && !prepared.WriteBuffer(0,0,0,bytes),"failed size validation must preserve uploaded state");
   rejected=false;try{VulkanPreparedBindings invalid(context,layouts,sizes,{0,0,1});}catch(const std::invalid_argument&){rejected=true;}
   RequireTest(rejected && prepared.Matches(layouts,sizes),"failed replacement must preserve prior preparation");
   sizes[0]=32;RequireTest(!prepared.Matches(layouts,sizes),"payload size invalidates layout");sizes[0]=16;
   layouts.push_back(layout);RequireTest(!prepared.Matches(layouts,sizes),"draw count invalidates layout");layouts.pop_back();
   context.InvalidateSampledResources();RequireTest(!prepared.Matches(layouts,sizes),"resource retirement invalidates descriptor ABA");
  }
  vkDestroySampler(context.GetDevice(),replacementSampler,nullptr);vkDestroySampler(context.GetDevice(),sampler,nullptr);vkDestroyDescriptorSetLayout(context.GetDevice(),layout,nullptr);
  RequireTest(context.GetValidationErrorCount()==0,"Vulkan validation errors");
  std::cout<<"PASS persistent bindings reuse, updates, schema and retirement invalidation\n";
 }catch(const std::exception& e){std::cerr<<e.what()<<'\n';return 1;}
}
