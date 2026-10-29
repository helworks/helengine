#pragma once
#include "vulkan_frame_trace.hpp"

namespace helengine::vulkan {
/// <summary>Ends a player-thread CPU wall-time scope at its real boundary or lexical exit without requiring renderer restructuring.</summary>
class VulkanFrameTraceScope final {
    /// <summary>Holds the recorder stack identity, or minus one after completion or while diagnostics are disabled.</summary>
    int Id;
public:
    /// <summary>Begins a static software scope only when an enabled player frame is being recorded.</summary>
    explicit VulkanFrameTraceScope(const char* name,std::source_location location=std::source_location::current()) : Id(VulkanFrameTrace::BeginScope(name,location)) { }
    /// <summary>Closes the scope at lexical exit; malformed scope nesting remains a visible diagnostic failure.</summary>
    ~VulkanFrameTraceScope() noexcept(false) { Finish(); }
    /// <summary>Prevents two owners from trying to close the same recorder stack entry.</summary>
    VulkanFrameTraceScope(const VulkanFrameTraceScope&)=delete;
    /// <summary>Prevents assignment from replacing an open stack entry.</summary>
    VulkanFrameTraceScope& operator=(const VulkanFrameTraceScope&)=delete;
    /// <summary>Counts measured items and owned payload bytes without inspecting resources when capture is disabled.</summary>
    void AddCounters(uint64_t items,uint64_t bytes) { VulkanFrameTrace::AddCounters(Id,items,bytes); }
    /// <summary>Marks only the item quantity as observed; byte measurements remain absent.</summary>
    void AddCounters(uint64_t items) { AddItems(items); }
    /// <summary>Accumulates an observed item quantity, preserving explicit zero as a measurement.</summary>
    void AddItems(uint64_t items) { VulkanFrameTrace::AddCounters(Id,items,0,1); }
    /// <summary>Accumulates observed payload bytes independently of whether items were measured.</summary>
    void AddBytes(uint64_t bytes) { VulkanFrameTrace::AddCounters(Id,0,bytes,2); }
    /// <summary>Associates this scope and future children with direct preparation or shared batch work.</summary>
    void SetContext(int subject,int batch=-1,const char* phase="preparation") { VulkanFrameTrace::SetContext(Id,subject,batch,phase); }
    /// <summary>Ends at an existing renderer phase boundary while retaining const locals in their current lifetime.</summary>
    void Finish() {
        const int id=Id; Id=-1;
        VulkanFrameTrace::EndScope(id);
    }
};
}
