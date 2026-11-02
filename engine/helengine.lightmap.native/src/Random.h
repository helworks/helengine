#pragma once
#include "Internal.h"

namespace hellm::detail {
/// <summary>SplitMix counter generator; each texel has an independent deterministic stream.</summary>
class Random {
    /// <summary>Per-texel counter state, independent of worker scheduling.</summary>
    uint64_t State;

  public:
    /// <summary>Initialize the counter generator with a scheduling-independent texel seed.</summary>
    explicit Random(uint64_t seed) : State(seed) {}
    /// <summary>Return the next deterministic uniform value in the half-open unit interval.</summary>
    double Next() {
        uint64_t z = (State += 0x9e3779b97f4a7c15ull);
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9ull;
        z = (z ^ (z >> 27)) * 0x94d049bb133111ebull;
        return double((z ^ (z >> 31)) >> 11) * 0x1.0p-53;
    }
};
} // namespace hellm::detail
