#pragma once
#include "Vec3.h"
#include <cstdint>
namespace helgi {
/// <summary>Explicit finite volume and deterministic integration quality; construction validates every value.</summary>
struct Config {
    /// <summary>Inclusive minimum corner of the world-space voxel volume.</summary>
    Vec3 Minimum;
    /// <summary>Exclusive maximum corner of the world-space voxel volume.</summary>
    Vec3 Maximum;
    /// <summary>Number of cells on X, from 1 through 128.</summary>
    std::uint32_t CountX;
    /// <summary>Number of cells on Y, from 1 through 128.</summary>
    std::uint32_t CountY;
    /// <summary>Number of cells on Z, from 1 through 128; total cells cannot exceed 262144.</summary>
    std::uint32_t CountZ;
    /// <summary>Cosine-distributed deterministic rays per hemisphere, from 1 through 256.</summary>
    std::uint32_t RaysPerLobe;
};
}
