#pragma once
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace helengine::vulkan {
/// <summary>Owns validated immutable submission slots independently from frame-local ordered references and profiler subjects.</summary>
template<class TSubmission>
class VulkanRetainedSubmissionRun final {
public:
    /// <summary>Keeps exact native world storage; a world change invalidates every retained slot before reuse.</summary>
    std::string WorldKey;
    /// <summary>Owns immutable input/material slots; queued frames can keep replaced slots alive independently.</summary>
    std::vector<std::shared_ptr<const TSubmission>> Submissions;
    /// <summary>Invalidates the run conservatively when camera, light or shadow preparation changes.</summary>
    uint64_t CameraGeneration=0;
    /// <summary>Invalidates the run conservatively when sampled GPU resource ownership changes.</summary>
    uint64_t ResourceGeneration=0;
    /// <summary>Records the latest complete-frame visit so unseen runs can retire without dereferencing historical scene pointers.</summary>
    uint64_t Epoch=0;
};
}
