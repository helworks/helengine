#pragma once

#include <atomic>
#include <cstdint>
#include <iomanip>
#include <sstream>
#include <string>
#include <string_view>

/// <summary>
/// Provides a lightweight Guid abstraction for transpiled managed code.
/// </summary>
class Guid {
    std::string value;

    static std::string FormatCounterGuid(std::uint64_t counter) {
        std::ostringstream builder;
        builder << std::hex << std::nouppercase << std::setw(32) << std::setfill('0') << counter;
        return builder.str();
    }

public:
    /// <summary>
    /// Initializes an all-zero guid value.
    /// </summary>
    Guid() : value("00000000000000000000000000000000") {}

    /// <summary>
    /// Initializes a guid from an existing text representation.
    /// </summary>
    /// <param name="text">Source text used for the guid value.</param>
    explicit Guid(std::string_view text) : value(text) {}

    /// <summary>
    /// Generates a unique guid-like token for generated content and asset ids.
    /// </summary>
    /// <returns>A unique Guid value.</returns>
    static Guid NewGuid() {
        static std::atomic<std::uint64_t> nextValue{1};
        std::uint64_t value = nextValue.fetch_add(1, std::memory_order_relaxed);
        return Guid(FormatCounterGuid(value));
    }

    /// <summary>
    /// Returns the guid text representation.
    /// </summary>
    /// <returns>Guid text.</returns>
    std::string ToString() const {
        return value;
    }

    /// <summary>
    /// Returns the guid text representation for the requested format.
    /// </summary>
    /// <param name="format">Requested formatting token.</param>
    /// <returns>Guid text.</returns>
    std::string ToString(std::string_view format) const {
        (void)format;
        return value;
    }
};
