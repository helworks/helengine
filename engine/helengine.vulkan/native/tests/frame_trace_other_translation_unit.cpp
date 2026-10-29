#include "vulkan_frame_trace_scope.hpp"

/// <summary>Records under the caller's current parent despite being compiled into a separate archive.</summary>
void RecordFrameTraceFromOtherTranslationUnit() {
    helengine::vulkan::VulkanFrameTraceScope scope("Update.OtherTranslationUnit");
}
