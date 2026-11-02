#pragma once
#include <cstdint>
#include <limits>
#include <stdexcept>
#include <string>
#include <utility>
namespace helengine::vulkan {
/// <summary>Versions exact successfully prepared camera/light/shadow state without lossy hashing or frame assumptions.</summary>
class VulkanPreparationGeneration final {
public:
    /// <summary>Returns zero until the first successful state publication.</summary>
    uint64_t Get() const {return Generation;}
    /// <summary>Publishes an already completed exact dependency key, advancing only when its bytes change.</summary>
    void Publish(std::string key) {
        if(Generation!=0 && Key==key) return;
        const auto next=Next(Generation);
        Key=std::move(key);Generation=next;
    }
    /// <summary>Rejects exhaustion instead of allowing old prepared snapshots to match a wrapped generation.</summary>
    static uint64_t Next(uint64_t generation) {
        if(generation==std::numeric_limits<uint64_t>::max()) throw std::overflow_error("Vulkan preparation generation exhausted.");
        return generation+1;
    }
private:
    /// <summary>Retains only the last successfully published exact camera state.</summary>
    std::string Key;
    /// <summary>Monotonically identifies the currently published key.</summary>
    uint64_t Generation=0;
};
}
