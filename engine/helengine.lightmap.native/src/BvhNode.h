#pragma once
#include "Bounds.h"
namespace hellm::detail {
/// <summary>One balanced tree node, using a contiguous triangle permutation for leaves.</summary>
struct BvhNode {
    /// <summary>World-space bounds containing all descendant triangles.</summary>
    Bounds Box;
    /// <summary>First child node index for internal nodes.</summary>
    uint32_t Left = 0;
    /// <summary>Second child node index for internal nodes.</summary>
    uint32_t Right = 0;
    /// <summary>First leaf element in the triangle permutation.</summary>
    uint32_t Begin = 0;
    /// <summary>Leaf element count; zero denotes an internal node.</summary>
    uint32_t Count = 0;
};
} // namespace hellm::detail
