#pragma once

/// <summary>
/// Minimal native placeholder for the managed IConvertible contract.
/// </summary>
class IConvertible {
public:
    /// <summary>
    /// Releases the placeholder contract through a polymorphic base destructor.
    /// </summary>
    virtual ~IConvertible() = default;
};
