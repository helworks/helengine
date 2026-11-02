#pragma once
#include "Hit.h"
#include <cstdint>
#include <vector>
namespace helgi {
/// <summary>Cached voxel intersections for one contributor revision.</summary>
struct Coverage {
    /// <summary>Revision of geometry represented by these intersections.</summary>
    std::uint64_t Revision;
    /// <summary>Independent intersections, preserved even when contributors overlap.</summary>
    std::vector<Hit> Hits;
};
}
