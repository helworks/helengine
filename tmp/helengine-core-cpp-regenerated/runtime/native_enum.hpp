#pragma once

/// <summary>
/// Represents the managed enum contract used only as a lightweight native placeholder.
/// </summary>
class Enum {
public:
    /// <summary>
    /// Releases the placeholder runtime contract through a polymorphic base destructor.
    /// </summary>
    virtual ~Enum() = default;
};
