#include "uniform_arena_layout.hpp"
#include <array>
#include <cstring>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>

/// <summary>Provides release-build assertions for real CPU uniform-layout behavior.</summary>
class UniformArenaLayoutTest final {
public:
    /// <summary>Reports the failed contract without relying on disabled release assertions.</summary>
    static void Require(bool valid,const char* message) { if(!valid) throw std::runtime_error(message); }
    /// <summary>Proves that offsets and reserved ranges preserve alignment and payload boundaries.</summary>
    static void PaddingAndPayload() {
        helengine::vulkan::UniformArenaLayout layout(256,65536,4096);
        Require(layout.GetSize()==0,"An empty frame must require no uniform allocation.");
        const std::array<uint64_t,4> sizes{224,288,400,16};
        const std::array<uint64_t,4> expected{0,256,768,1280};
        std::array<uint64_t,4> offsets{};
        for(size_t index=0;index<sizes.size();++index) {
            offsets[index]=layout.Add(sizes[index]);
            Require(offsets[index]==expected[index],"Uniform descriptor offsets must include alignment padding.");
        }
        Require(layout.GetSize()==1536,"The arena must reserve aligned slices including the final slice.");
        std::vector<uint8_t> arena(layout.GetSize(),0xcc);
        for(size_t index=0;index<sizes.size();++index) std::memset(arena.data()+offsets[index],static_cast<int>(index+1),sizes[index]);
        for(size_t index=0;index<sizes.size();++index) {
            for(uint64_t byte=0;byte<sizes[index];++byte) Require(arena[offsets[index]+byte]==index+1,"Aligned uniform slices must preserve every copied payload without overlap.");
            Require(arena[offsets[index]+sizes[index]]==0xcc,"Uniform padding must stay outside the payload range.");
        }
    }
    /// <summary>Rejects invalid device limits, empty or oversized bindings and capacities before mutation.</summary>
    static void InvalidRanges() {
        bool rejected=false;
        try { helengine::vulkan::UniformArenaLayout invalid(0,65536,4096); } catch(const std::invalid_argument&) { rejected=true; }
        Require(rejected,"A missing device alignment must be rejected.");
        rejected=false;
        try { helengine::vulkan::UniformArenaLayout invalid(256,0,4096); } catch(const std::invalid_argument&) { rejected=true; }
        Require(rejected,"A missing device uniform range must be rejected.");
        helengine::vulkan::UniformArenaLayout layout(256,288,512);
        for(uint64_t bytes:std::array<uint64_t,2>{0,289}) {
            rejected=false;
            try { layout.Add(bytes); } catch(const std::invalid_argument&) { rejected=true; }
            Require(rejected && layout.GetSize()==0,"Invalid payload ranges must fail without changing the arena.");
        }
        Require(layout.Add(288)==0 && layout.GetSize()==512,"A maximum-range payload must fit its padded capacity exactly.");
        rejected=false;
        try { layout.Add(1); } catch(const std::length_error&) { rejected=true; }
        Require(rejected && layout.GetSize()==512,"Host allocation bounds must be checked before growing the arena.");
    }
    /// <summary>Replays the actual Dota draw payload sizes and checks every slice plus the approved arena budget.</summary>
    static void DotaFrame() {
        helengine::vulkan::UniformArenaLayout layout(256,65536,std::numeric_limits<size_t>::max());
        const std::array<uint64_t,8> sizes{224,16,4,4,4,16,288,400};
        uint64_t previousEnd=0;
        size_t bindings=0;
        for(size_t draw=0;draw<21940;++draw) {
            for(const uint64_t size:sizes) {
                const uint64_t offset=layout.Add(size);
                Require(offset%256==0 && offset>=previousEnd,"Every Dota uniform binding must have an aligned, nonoverlapping payload.");
                previousEnd=offset+size;
                ++bindings;
            }
        }
        Require(bindings==175520 && layout.GetSize()==56166400,"The 21940-draw Dota frame must fit the approved 56166400-byte arena.");
    }
    /// <summary>Checks both rounding overflow and accumulated-size overflow using CPU-only boundary values.</summary>
    static void Overflow() {
        constexpr auto maximum=std::numeric_limits<uint64_t>::max();
        helengine::vulkan::UniformArenaLayout rounding(256,maximum,maximum);
        bool rejected=false;
        try { rounding.Add(maximum); } catch(const std::length_error&) { rejected=true; }
        Require(rejected && rounding.GetSize()==0,"Padding must reject uint64 overflow before allocation.");
        helengine::vulkan::UniformArenaLayout total(1,maximum,maximum);
        Require(total.Add(maximum)==0,"An unpadded uint64 range must remain representable.");
        rejected=false;
        try { total.Add(1); } catch(const std::length_error&) { rejected=true; }
        Require(rejected && total.GetSize()==maximum,"Accumulated uniform size must reject uint64 overflow without mutation.");
    }
};

/// <summary>Exercises padding, byte preservation and range/overflow rejection without a Vulkan device.</summary>
int main() {
    try {
        UniformArenaLayoutTest::PaddingAndPayload();
        UniformArenaLayoutTest::InvalidRanges();
        UniformArenaLayoutTest::Overflow();
        UniformArenaLayoutTest::DotaFrame();
        std::cout<<"Uniform arena padding, payload isolation, ranges, overflow and Dota 175520 bindings / 56166400 bytes passed\n";
    } catch(const std::exception& error) { std::cerr<<error.what()<<'\n'; return 1; }
}
