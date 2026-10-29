#pragma once
#include <cstdint>
#include <limits>
#include <stdexcept>

namespace helengine::vulkan {
/// <summary>Plans aligned, range-checked slices before allocating a synchronous frame uniform buffer.</summary>
class UniformArenaLayout final {
public:
    /// <summary>Accepts the device offset alignment, per-binding range and host-addressable arena capacity.</summary>
    UniformArenaLayout(uint64_t alignment,uint64_t maximumRange,uint64_t maximumBytes) : Alignment(alignment), MaximumRange(maximumRange), MaximumBytes(maximumBytes) {
        if(!Alignment || !MaximumRange) throw std::invalid_argument("Uniform arena requires nonzero device alignment and maximum binding range");
    }
    /// <summary>Reserves one payload rounded to the device alignment and returns its offset; rejected slices leave the layout unchanged.</summary>
    uint64_t Add(uint64_t bytes) {
        if(!bytes || bytes>MaximumRange) throw std::invalid_argument("Uniform payload must be nonempty and within the device binding range");
        const uint64_t remainder=bytes%Alignment;
        const uint64_t padding=remainder ? Alignment-remainder : 0;
        if(bytes>std::numeric_limits<uint64_t>::max()-padding) throw std::length_error("Uniform slice alignment overflows uint64");
        const uint64_t reserved=bytes+padding;
        if(reserved>MaximumBytes-Bytes) throw std::length_error("Uniform arena exceeds its allocation or address-space limit");
        const uint64_t offset=Bytes;
        Bytes+=reserved;
        return offset;
    }
    /// <summary>Returns the total allocation size required by all planned slices.</summary>
    uint64_t GetSize() const { return Bytes; }
private:
    /// <summary>Stores the required descriptor offset granularity.</summary>
    uint64_t Alignment;
    /// <summary>Bounds each descriptor's actual payload range.</summary>
    uint64_t MaximumRange;
    /// <summary>Bounds the arena by the caller's allocation and address-space limit.</summary>
    uint64_t MaximumBytes;
    /// <summary>Stores the bytes reserved by the planned slices.</summary>
    uint64_t Bytes=0;
};
}
