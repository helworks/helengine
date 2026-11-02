#pragma once
#include "helgi/Triangle.h"
#include <cstdint>
#include <vector>
namespace helgi {
/// <summary>Immutable submitted geometry whose lifetime spans an in-flight snapshot.</summary>
struct Contributor {
    /// <summary>Caller-owned change identifier.</summary>
    std::uint64_t Revision;
    /// <summary>Copied validated world-space triangles.</summary>
    std::vector<Triangle> Triangles;
};
}
