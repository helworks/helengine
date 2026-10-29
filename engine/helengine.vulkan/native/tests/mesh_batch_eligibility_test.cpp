#include "vulkan_mesh_batch_eligibility.hpp"
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>

/// <summary>Exercises the production eligibility predicate with actual editor-produced standard material fields and unsupported contracts.</summary>
class MeshBatchEligibilityTest final {
public:
    /// <summary>Reports the exact authored-contract case whose eligibility differs from its independent expected outcome.</summary>
    static void Require(std::string_view name,std::string_view shaderId,std::string_view vertexProgram,std::string_view pixelProgram,std::string_view variant,uint32_t blendMode,bool expected) {
        if(helengine::vulkan::VulkanMeshBatchEligibility::Supports(shaderId,vertexProgram,pixelProgram,variant,blendMode)!=expected) throw std::runtime_error(std::string(name));
    }
    /// <summary>Guards the producer default regression while keeping custom shader/program/variant and blended materials outside batching.</summary>
    static void Run() {
        Require("Editor-produced opaque default material must be eligible","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","default",0,true);
        Require("Named standard variant must remain eligible","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","ForwardStandard",0,true);
        Require("Named shadowed standard variant must remain eligible","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","ForwardStandardShadowed",0,true);
        Require("Custom shader must be rejected","CustomShader","ForwardStandardShader.vs","ForwardStandardShader.ps","default",0,false);
        Require("Custom vertex program must be rejected","ForwardStandardShader","Custom.vs","ForwardStandardShader.ps","default",0,false);
        Require("Custom pixel program must be rejected","ForwardStandardShader","ForwardStandardShader.vs","Custom.ps","default",0,false);
        Require("Custom variant must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","custom",0,false);
        Require("Empty shader must be rejected","","ForwardStandardShader.vs","ForwardStandardShader.ps","default",0,false);
        Require("Empty vertex program must be rejected","ForwardStandardShader","","ForwardStandardShader.ps","default",0,false);
        Require("Empty pixel program must be rejected","ForwardStandardShader","ForwardStandardShader.vs","","default",0,false);
        Require("Empty variant must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","",0,false);
        Require("Variant names must retain exact case","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","Default",0,false);
        Require("Alpha-blended default material must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","default",1,false);
        Require("Additive default material must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","default",2,false);
        Require("Blended named standard material must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","ForwardStandard",1,false);
        Require("Blended shadowed standard material must be rejected","ForwardStandardShader","ForwardStandardShader.vs","ForwardStandardShader.ps","ForwardStandardShadowed",1,false);
    }
};

/// <summary>Returns a failing CPU-test process status for any regression in real material eligibility.</summary>
int main() {
    try { MeshBatchEligibilityTest::Run(); std::cout<<"PASS native material batching eligibility (16 contracts)\n"; }
    catch(const std::exception& error) { std::cerr<<error.what()<<'\n'; return 1; }
}
