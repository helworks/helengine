#pragma once
#include <cstdint>
namespace helgi {
/// <summary>Cumulative deterministic work counters, including work still private to a staged update.</summary>
struct Stats {
    /// <summary>Total conservative triangle versus voxel tests since construction.</summary>
    std::uint64_t TriangleVoxelTests;
    /// <summary>Total traced visibility and bounce rays since construction.</summary>
    std::uint64_t RaysTraced;
    /// <summary>Total completely published field generations.</summary>
    std::uint64_t Publications;
    /// <summary>Most recently submitted input generation.</summary>
    std::uint64_t InputGeneration;
    /// <summary>True while a private generation is being processed or newer inputs await processing.</summary>
    bool UpdatePending;
};
}
