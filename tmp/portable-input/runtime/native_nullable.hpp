#pragma once

#include <cstddef>

/// <summary>
/// Represents a lightweight managed nullable value wrapper for transpiled code on constrained native targets.
/// </summary>
template <typename T>
class Nullable {
public:
    /// <summary>
    /// Initializes the wrapper without a value.
    /// </summary>
    Nullable()
        : HasValue(false), Value() {
    }

    /// <summary>
    /// Initializes the wrapper with an explicit value.
    /// </summary>
    /// <param name="value">Value captured by the nullable wrapper.</param>
    Nullable(const T& value)
        : HasValue(true), Value(value) {
    }

    /// <summary>
    /// Initializes the wrapper as empty from a null literal.
    /// </summary>
    /// <param name="value">Ignored null literal used by transpiled default values and assignments.</param>
    Nullable(std::nullptr_t value)
        : HasValue(false), Value() {
        (void)value;
    }

    /// <summary>
    /// Assigns a concrete value and marks the wrapper as populated.
    /// </summary>
    /// <param name="value">Value captured by the wrapper.</param>
    /// <returns>The current wrapper for chained assignments.</returns>
    Nullable& operator=(const T& value) {
        HasValue = true;
        Value = value;
        return *this;
    }

    /// <summary>
    /// Clears the wrapper when a null literal is assigned.
    /// </summary>
    /// <param name="value">Ignored null literal used by transpiled assignments.</param>
    /// <returns>The current wrapper for chained assignments.</returns>
    Nullable& operator=(std::nullptr_t value) {
        (void)value;
        HasValue = false;
        Value = T();
        return *this;
    }

    /// <summary>
    /// Gets whether the wrapper currently contains a value.
    /// </summary>
    bool HasValue;

    /// <summary>
    /// Gets or sets the wrapped value used by transpiled code paths.
    /// </summary>
    T Value;
};
