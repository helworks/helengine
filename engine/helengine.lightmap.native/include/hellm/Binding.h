#pragma once
#include <hellm/Vec2.h>
#include <string>
#include <vector>

namespace hellm {
/// <summary>UV coordinates in original mesh index/corner order, normalized to the atlas.</summary>
struct Binding {
    /// <summary>Stable identity of the source drawable submesh.</summary>
    std::string Key;
    /// <summary>One normalized UV for each original index, preserving triangle and corner order.</summary>
    std::vector<Vec2> CornerUvs;
};
} // namespace hellm
