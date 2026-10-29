#include "vulkan_mesh_visit_snapshots.hpp"
#include <iostream>
#include <stdexcept>
using namespace helengine::vulkan;
/// <summary>Rejects incorrect production snapshot lifetime, reuse or effective-state behavior.</summary>
static void Require(bool value,const char* message) { if(!value) throw std::runtime_error(message); }
/// <summary>Creates a real reflected binding snapshot with independently mutable input bytes.</summary>
static std::shared_ptr<const VulkanMeshMaterialSnapshot> Create(uint8_t value,size_t pipeline=7) {
    std::vector<VulkanMeshRenderer::BufferBinding> original{{0,std::vector<uint8_t>(224,0)},{3,{value,0,0,0}}};
    auto baked=original;
    return std::make_shared<const VulkanMeshMaterialSnapshot>(pipeline,12,true,std::move(original),std::move(baked),std::vector<VulkanMeshRenderer::ImageBinding>{});
}
/// <summary>Exercises the same visit-local factory and immutable shared payload used by the generated-core bridge.</summary>
int main() {
    try {
        int materialA=0,materialB=0,builds=0; uint8_t value=23;
        std::weak_ptr<const VulkanMeshMaterialSnapshot> weak;
        std::shared_ptr<const VulkanMeshMaterialSnapshot> submission;
        {
            VulkanMeshVisitSnapshots visit;
            auto a=visit.GetOrCreate(&materialA,[&] { ++builds; return Create(value); });
            auto b=visit.GetOrCreate(&materialA,[&] { ++builds; return Create(99); });
            Require(a==b && builds==1,"same visit/material rebuilt its payload");
            auto other=visit.GetOrCreate(&materialB,[&] { ++builds; return Create(value); });
            Require(other!=a && other->State==a->State && builds==2,"material identity incorrectly replaced effective state compatibility");
            submission=a; weak=a;
            Require(a->Buffers[1].Bytes[0]==23 && a->BakedBuffers[1].Bytes[0]==23,"snapshot lost original/baked bindings");
            Require(a->GetPayloadBytes()==456,"snapshot payload byte accounting changed");
            bool failed=false;
            int broken=0;
            try { visit.GetOrCreate(&broken,[]()->std::shared_ptr<const VulkanMeshMaterialSnapshot> { throw std::runtime_error("constructor failed"); }); } catch(const std::runtime_error&) { failed=true; }
            Require(failed,"snapshot construction failure was masked");
            auto retry=visit.GetOrCreate(&broken,[&] { ++builds;return Create(42); });
            Require(retry->Buffers[1].Bytes[0]==42 && builds==3,"failed construction published a partial cache entry");
            failed=false;
            try { visit.GetOrCreate(nullptr,[] { return Create(0); }); } catch(const std::invalid_argument&) { failed=true; }
            Require(failed,"null material identity was accepted");
            failed=false;
            int empty=0;
            try { visit.GetOrCreate(&empty,[] { return std::shared_ptr<const VulkanMeshMaterialSnapshot>{}; }); } catch(const std::invalid_argument&) { failed=true; }
            Require(failed,"null snapshot was published");
        }
        Require(!weak.expired(),"submissions lost payload when visit table ended");
        value=77;
        {
            VulkanMeshVisitSnapshots nextObjectOrPass;
            auto fresh=nextObjectOrPass.GetOrCreate(&materialA,[&] { return Create(value); });
            Require(fresh!=submission && fresh->State!=submission->State && fresh->CacheState!=submission->CacheState,"next visit reused stale mutable material state");
            Require(submission->Buffers[1].Bytes[0]==23,"new visit mutated the queued previous payload");
        }
        submission.reset();Require(weak.expired(),"last submission did not release payload");
        // Camera matrices affect pass compatibility, but never persistent baked geometry identity.
        auto one=Create(5); auto original=one->Buffers,baked=one->BakedBuffers;
        baked[0].Bytes[0]=1;
        const VulkanMeshMaterialSnapshot camera(7,12,true,original,baked,{});
        Require(camera.State!=one->State && camera.CacheState==one->CacheState,"camera bytes entered persistent geometry identity or left pass compatibility");
        baked[0].Bytes[208]=1;
        const VulkanMeshMaterialSnapshot flags(7,12,true,original,baked,{});
        Require(flags.CacheState!=one->CacheState,"material transform flags were omitted from geometry identity");
        Require(Create(5,99)->State==one->State,"equivalent pipeline identity was replaced by owned pipeline handle");
        std::cout << "PASS immutable visit snapshots: reuse isolation failure lifetime effective-state keys\n";return 0;
    } catch(const std::exception& error) { std::cerr << error.what() << '\n';return 1; }
}
