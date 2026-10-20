#pragma once
#include <atomic>
#include <stdint.h>
#include <stdexcept>

/// <summary>Counts pending and leased frames without owning them or retaining the session.</summary>
class CaptureFrameBudget {
    /// <summary>Maximum number of live frame textures.</summary>
    const uint32_t Capacity;
    /// <summary>Number of successful reservations not yet returned by frame destruction.</summary>
    std::atomic<uint32_t> InUse { 0 };
public:
    /// <summary>Rejects an empty budget instead of creating an unusable session.</summary>
    explicit CaptureFrameBudget(uint32_t capacity) : Capacity(capacity) {
        if (!capacity) { throw std::invalid_argument("BufferCapacity must be positive."); }
    }
    /// <summary>Reserves capacity before a texture is allocated.</summary>
    bool TryReserve() {
        auto current = InUse.load();
        while (current < Capacity) { if (InUse.compare_exchange_weak(current, current + 1)) { return true; } }
        return false;
    }
    /// <summary>Returns a reservation on frame destruction or failed initialization.</summary>
    void Release() noexcept { InUse.fetch_sub(1); }
};
