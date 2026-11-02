#pragma once
#include <cstdint>
#include <vector>
namespace helgi {
/// <summary>Completed immutable-until-next-publication six-direction irradiance atlas.</summary>
struct Field {
    /// <summary>Published input generation; zero denotes the initial dark field.</summary>
    std::uint64_t Generation;
    /// <summary>Atlas width, CountX times CountY.</summary>
    std::uint32_t Width;
    /// <summary>Atlas height, CountZ times six.</summary>
    std::uint32_t Height;
    /// <summary>Row-major RGBA32F: alpha 1 marks empty irradiance probes, 0 uniformly coplanar occupied cells, and -1 mixed occupied cells. Uniform occupied lobe 0 RGB stores a canonical plane normal; lobe 1 R stores its signed plane offset. Other occupied RGB is unused.</summary>
    std::vector<float> AtlasRgba;
};
}

