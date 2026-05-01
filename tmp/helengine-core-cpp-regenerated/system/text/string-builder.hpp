#pragma once

#include <string>
#include <string_view>
#include <cstddef>

/// <summary>
/// Provides a lightweight append-oriented string builder for transpiled managed code.
/// </summary>
class StringBuilder {
    std::string buffer;

public:
    /// <summary>
    /// Initializes an empty builder.
    /// </summary>
    StringBuilder() = default;

    /// <summary>
    /// Initializes a builder with a reserved capacity hint.
    /// </summary>
    /// <param name="capacity">Expected character capacity for the composed string.</param>
    explicit StringBuilder(int capacity) {
        if (capacity > 0) {
            buffer.reserve(static_cast<std::size_t>(capacity));
        }
    }

    /// <summary>
    /// Appends a single character and returns the builder for chaining.
    /// </summary>
    /// <param name="value">Character to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(char value) {
        buffer.push_back(value);
        return *this;
    }

    /// <summary>
    /// Appends a string view and returns the builder for chaining.
    /// </summary>
    /// <param name="value">String content to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(std::string_view value) {
        buffer.append(value);
        return *this;
    }

    /// <summary>
    /// Appends an integer value and returns the builder for chaining.
    /// </summary>
    /// <param name="value">Integer value to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(int value) {
        buffer.append(std::to_string(value));
        return *this;
    }

    /// <summary>
    /// Appends a newline sequence and returns the builder for chaining.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    StringBuilder& AppendLine() {
        buffer.push_back('\n');
        return *this;
    }

    /// <summary>
    /// Appends a string followed by a newline sequence and returns the builder for chaining.
    /// </summary>
    /// <param name="value">String content to append before the newline.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& AppendLine(std::string_view value) {
        buffer.append(value);
        buffer.push_back('\n');
        return *this;
    }

    /// <summary>
    /// Materializes the built string value.
    /// </summary>
    /// <returns>A copy of the accumulated string content.</returns>
    std::string ToString() const {
        return buffer;
    }
};
