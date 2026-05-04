#pragma once

/// <summary>
/// Minimal native placeholder for the managed ISpanFormattable contract.
/// </summary>
class ISpanFormattable {
public:
    /// <summary>
    /// Releases the placeholder contract through a polymorphic base destructor.
    /// </summary>
    virtual ~ISpanFormattable() = default;
};
