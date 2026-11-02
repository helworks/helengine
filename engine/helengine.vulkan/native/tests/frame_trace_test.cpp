#include "vulkan_frame_trace_scope.hpp"
#include <cstdlib>
#include <iostream>
#include <sstream>
#include <stdexcept>

/// <summary>Creates a scope in another translation unit to prove all renderer archives share one player-thread recorder.</summary>
void RecordFrameTraceFromOtherTranslationUnit();

/// <summary>Exercises real monotonic nested recording, disabled no-export behavior, and fail-fast frame limits without a GPU.</summary>
int main(int argc,char** argv) {
    using helengine::vulkan::VulkanFrameTrace;
    using helengine::vulkan::VulkanFrameTraceScope;
    try {
        const bool enabled=argc>1 && std::string(argv[1])=="enabled";
        if(enabled) setenv("HELENGINE_FRAME_TRACE","1",1); else unsetenv("HELENGINE_FRAME_TRACE");
        std::ostringstream output;
        VulkanFrameTrace::BeginFrame(7);
        int firstSubject=-1,secondSubject=-1;
        if(VulkanFrameTrace::IsRecording()) {
            firstSubject=VulkanFrameTrace::RegisterSubject(&output,42,"model\"quoted","material\\path",8,0,6);
            secondSubject=VulkanFrameTrace::RegisterSubject(&output,42,"model\"quoted","material\\path",8,1,3);
            if(firstSubject!=VulkanFrameTrace::RegisterSubject(&output,42,"model\"quoted","material\\path",8,0,6)) throw std::runtime_error("Subject was not deduplicated.");
        }
        {
            VulkanFrameTraceScope update("Core.Update");
            update.SetContext(firstSubject);
            { VulkanFrameTraceScope child("Update.Child"); child.AddCounters(3,224); }
            const auto end=std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now().time_since_epoch()).count();
            VulkanFrameTrace::RecordCompletedSample("Type\"quoted\\name",end,end,123,1,2,0);
            RecordFrameTraceFromOtherTranslationUnit();
            update.Finish();
            update.Finish();
        }
        { VulkanFrameTraceScope draw("Core.Draw"); draw.AddItems(0);
            if(VulkanFrameTrace::IsRecording()) draw.SetContext(-1,VulkanFrameTrace::RegisterBatch({firstSubject,secondSubject,firstSubject}),"emission");
        }
        VulkanFrameTrace::EndFrame(output,23,19,17,3,1920,1080,true);
        if(!enabled) {
            if(!output.str().empty()) throw std::runtime_error("Disabled trace exported a frame.");
            VulkanFrameTraceScope invalid("ignored invalid name");
            std::cout << "PASS disabled frame trace\n";
            return 0;
        }
        if(output.str().find("HELENGINE_FRAME_TRACE {\"frame\":7,")==std::string::npos || output.str().find("\"rootHits\":17,\"rootMisses\":3")==std::string::npos) throw std::runtime_error("Trace JSON lost the frame identity or real metrics.");
        if(output.str().find("\"counters\":{\"items\":3,\"bytes\":224}")==std::string::npos || output.str().find("\"cpuSampleResolutionNs\":1000")==std::string::npos || output.str().find("\"registrationId\":1")==std::string::npos) throw std::runtime_error("Trace lost exact counters or completed CPU sample metadata.");
        std::cout << output.str();
        bool overflow=false;
        VulkanFrameTrace::BeginFrame(8);
        try { for(int index=0;index<262144;++index) { VulkanFrameTraceScope event("Budget.Scope"); } }
        catch(const std::overflow_error&) { overflow=true; }
        if(!overflow) throw std::runtime_error("Frame span overflow was silently truncated.");
        bool nesting=false;
        VulkanFrameTrace::BeginFrame(9);
        const auto parent=VulkanFrameTrace::BeginScope("Parent");
        VulkanFrameTrace::BeginScope("Child");
        try { VulkanFrameTrace::EndScope(parent); } catch(const std::logic_error&) { nesting=true; }
        if(!nesting) throw std::runtime_error("Invalid nesting was accepted.");
        bool unfinished=false;
        VulkanFrameTrace::BeginFrame(10);
        VulkanFrameTrace::BeginScope("Unfinished");
        try { VulkanFrameTrace::EndFrame(output,0,0,0,0,1,1,false); } catch(const std::logic_error&) { unfinished=true; }
        if(!unfinished) throw std::runtime_error("An incomplete frame was exported.");
        bool unsafe=false;
        VulkanFrameTrace::BeginFrame(11);
        try { VulkanFrameTrace::BeginScope("unsafe\"name"); } catch(const std::invalid_argument&) { unsafe=true; }
        if(!unsafe) throw std::runtime_error("Unsafe scope metadata was accepted.");
        VulkanFrameTrace::BeginFrame(12);
        { VulkanFrameTraceScope events("Host.Events"); VulkanFrameTrace::DiscardFrame(); }
        VulkanFrameTrace::BeginFrame(13);
        VulkanFrameTrace::EndFrame(std::cout,0,0,0,0,1,1,false);
        std::cout << "PASS enabled frame trace nested bounds shared TLS JSON overflow invalid nesting\n";
        return 0;
    } catch(const std::exception& error) { std::cerr << "FAIL: " << error.what() << '\n'; return 1; }
}
