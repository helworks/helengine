#pragma once

/// <summary>
/// Minimal native placeholder for the managed IFormattable contract.
/// </summary>
class IFormattable {
public:
    /// <summary>
    /// Releases the placeholder contract through a polymorphic base destructor.
    /// </summary>
    virtual ~IFormattable() = default;
};
