#pragma once
#include "BvhNode.h"
#include "Hit.h"
#include "Triangle.h"

namespace hellm::detail {
/// <summary>Balanced immutable BVH; traversal is safe to share across worker threads.</summary>
class Bvh {
    /// <summary>Referenced world triangles whose lifetime spans all tracing workers.</summary>
    const std::vector<Triangle> &Triangles;
    /// <summary>Deterministic spatial permutation, preserving original identities.</summary>
    std::vector<uint32_t> Order;
    /// <summary>Contiguous balanced nodes rooted at index zero.</summary>
    std::vector<BvhNode> Nodes;
    /// <summary>Scale-dependent exclusion distance for ray-origin self intersections.</summary>
    double Epsilon;
    /// <summary>Construct a deterministic balanced median-split subtree over triangle centroids.</summary>
    uint32_t Build(uint32_t begin, uint32_t end);
    /// <summary>Walk immutable BVH nodes, returning either the nearest intersection or the first blocker.</summary>
    bool Traverse(Vec3 origin, Vec3 direction, Hit &hit, bool anyHit) const;

  public:
    /// <summary>Initialize the triangle permutation and balanced BVH once before parallel tracing.</summary>
    Bvh(const std::vector<Triangle> &triangles, double epsilon);
    /// <summary>Find the closest two-sided intersection beyond the origin exclusion distance.</summary>
    bool Intersect(Vec3 origin, Vec3 direction, Hit &hit) const;
    /// <summary>Stop at the first two-sided blocker along the directional sun ray.</summary>
    bool Occluded(Vec3 origin, Vec3 direction) const;
};
} // namespace hellm::detail
