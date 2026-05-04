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
    int Length;

    /// <summary>
    /// Initializes an empty builder.
    /// </summary>
    StringBuilder() : Length(0) {}

    /// <summary>
    /// Initializes a builder with a reserved capacity hint.
    /// </summary>
    /// <param name="capacity">Expected character capacity for the composed string.</param>
    explicit StringBuilder(int capacity) {
        if (capacity > 0) {
            buffer.reserve(static_cast<std::size_t>(capacity));
        }
        Length = 0;
    }

    /// <summary>
    /// Initializes a builder from an existing string value.
    /// </summary>
    /// <param name="value">Initial text content for the builder.</param>
    explicit StringBuilder(std::string_view value) {
        buffer.append(value);
        Length = static_cast<int>(buffer.size());
    }

    /// <summary>
    /// Appends a single character and returns the builder for chaining.
    /// </summary>
    /// <param name="value">Character to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(char value) {
        buffer.push_back(value);
        Length = static_cast<int>(buffer.size());
        return *this;
    }

    /// <summary>
    /// Appends a string view and returns the builder for chaining.
    /// </summary>
    /// <param name="value">String content to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(std::string_view value) {
        buffer.append(value);
        Length = static_cast<int>(buffer.size());
        return *this;
    }

    /// <summary>
    /// Appends an integer value and returns the builder for chaining.
    /// </summary>
    /// <param name="value">Integer value to append.</param>
    /// <returns>The current builder instance.</returns>
    StringBuilder& Append(int value) {
        buffer.append(std::to_string(value));
        Length = static_cast<int>(buffer.size());
        return *this;
    }

    /// <summary>
    /// Appends a newline sequence and returns the builder for chaining.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    StringBuilder& AppendLine() {
        buffer.push_back('\n');
        Length = static_cast<int>(buffer.size());
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
        Length = static_cast<int>(buffer.size());
        return *this;
    }

    /// <summary>
    /// Truncates the builder to the specified length.
    /// </summary>
    void set_Length(int value) {
        if (value < 0) {
            value = 0;
        }

        std::size_t size = static_cast<std::size_t>(value);
        if (size < buffer.size()) {
            buffer.resize(size);
        }
        Length = static_cast<int>(buffer.size());
    }

    /// <summary>
    /// Materializes a substring of the built string value.
    /// </summary>
    /// <param name="startIndex">Start index of the substring.</param>
    /// <param name="length">Length of the substring.</param>
    /// <returns>A substring of the accumulated string content.</returns>
    std::string ToString(int startIndex, int length) const {
        if (startIndex < 0) {
            startIndex = 0;
        }
        if (length < 0) {
            length = 0;
        }

        std::size_t start = static_cast<std::size_t>(startIndex);
        if (start >= buffer.size()) {
            return std::string();
        }

        std::size_t count = static_cast<std::size_t>(length);
        if (start + count > buffer.size()) {
            count = buffer.size() - start;
        }

        return buffer.substr(start, count);
    }

    /// <summary>
    /// Materializes the built string value.
    /// </summary>
    /// <returns>A copy of the accumulated string content.</returns>
    std::string ToString() const {
        return buffer;
    }
};
